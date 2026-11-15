using FamilyMoney.Configuration;
using FamilyMoney.Sync;
using FamilyMoney.ViewModels.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FamilyMoney.Tests;

public sealed class SyncConfigurationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "FamilyMoneySyncConfigTests",
        Guid.NewGuid().ToString("N"));

    public SyncConfigurationTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void LegacyS3Configuration_IsMigratedToSyncProvider()
    {
        var database = new DatabaseConfiguration
        {
            S3 = new S3StorageConfiguration
            {
                Enabled = true,
                Bucket = "bucket",
                AccessKey = "access",
                SecretKey = "secret",
            },
        };

        var sync = database.GetSyncConfiguration();

        Assert.Equal(SyncProvider.S3, sync.Provider);
        Assert.True(sync.Enabled);
        Assert.Same(database.S3, sync.S3);
        Assert.True(sync.IsConfigured);
    }

    [Fact]
    public void QrPayloadV2_DoesNotContainSecrets()
    {
        var database = new DatabaseConfiguration
        {
            SyncId = Guid.NewGuid(),
            Name = "Семейный бюджет",
            Path = "database.db",
            BackupsFolder = "Backups",
            Sync = new SyncStorageConfiguration
            {
                Provider = SyncProvider.S3,
                Enabled = true,
                S3 = new S3StorageConfiguration
                {
                    ServiceUrl = "https://storage.yandexcloud.net",
                    Bucket = "bucket",
                    Region = "ru-central1",
                    AccessKey = "s3-access-key",
                    SecretKey = "s3-secret-key",
                },
            },
        };

        var json = DatabaseQrPayload.FromConfiguration(database).ToJson();

        Assert.Contains("\"Version\":2", json);
        Assert.DoesNotContain("s3-access-key", json);
        Assert.DoesNotContain("s3-secret-key", json);

        var parsed = DatabaseQrPayload.TryParse(json);
        Assert.NotNull(parsed);

        var restored = parsed!.ToConfiguration();
        Assert.Equal(SyncProvider.S3, restored.Sync!.Provider);
        Assert.Equal("bucket", restored.Sync.S3.Bucket);
        Assert.Empty(restored.Sync.S3.AccessKey);
        Assert.Empty(restored.Sync.S3.SecretKey);
    }

    [Fact]
    public void QrPayloadV2_DoesNotContainWebDavPassword()
    {
        var database = new DatabaseConfiguration
        {
            SyncId = Guid.NewGuid(),
            Name = "WebDAV",
            Path = "database.db",
            BackupsFolder = "Backups",
            Sync = new SyncStorageConfiguration
            {
                Provider = SyncProvider.WebDav,
                Enabled = true,
                WebDav = new WebDavStorageConfiguration
                {
                    Url = "https://webdav.cloud.mail.ru",
                    UserName = "family@example.com",
                    Password = "webdav-app-password",
                },
            },
        };

        var json = DatabaseQrPayload.FromConfiguration(database).ToJson();

        Assert.DoesNotContain("webdav-app-password", json);
        var restored = DatabaseQrPayload.TryParse(json)!.ToConfiguration();
        Assert.Equal(SyncProvider.WebDav, restored.Sync!.Provider);
        Assert.Equal("family@example.com", restored.Sync.WebDav.UserName);
        Assert.Empty(restored.Sync.WebDav.Password);
    }

    [Fact]
    public void QrPayload_CanExplicitlyIncludeSecretsForPhoneSetup()
    {
        var database = new DatabaseConfiguration
        {
            SyncId = Guid.NewGuid(),
            Name = "С ключами",
            Sync = new SyncStorageConfiguration
            {
                Provider = SyncProvider.WebDav,
                Enabled = true,
                WebDav = new WebDavStorageConfiguration
                {
                    Url = "https://webdav.cloud.mail.ru",
                    UserName = "family@example.com",
                    Password = "webdav-app-password",
                },
            },
        };

        var json = DatabaseQrPayload.FromConfiguration(database, includeSecrets: true).ToJson();

        Assert.Contains("webdav-app-password", json);
        var restored = DatabaseQrPayload.TryParse(json)!.ToConfiguration();
        Assert.Equal("webdav-app-password", restored.Sync!.WebDav.Password);
    }

    [Fact]
    public void LegacyQrPayload_StillRestoresS3Settings()
    {
        const string json =
            "{\"App\":\"familymoney\",\"Version\":1,\"Name\":\"Legacy\"," +
            "\"S3\":{\"Enabled\":true,\"Bucket\":\"bucket\"," +
            "\"AccessKey\":\"access\",\"SecretKey\":\"secret\"}}";

        var payload = DatabaseQrPayload.TryParse(json);
        Assert.NotNull(payload);

        var database = payload!.ToConfiguration();
        var sync = database.GetSyncConfiguration();

        Assert.Equal(SyncProvider.S3, sync.Provider);
        Assert.True(sync.Enabled);
        Assert.Equal("secret", sync.S3.SecretKey);
    }

    [Fact]
    public void ProviderValidation_ChecksOnlySelectedProvider()
    {
        var s3 = new SyncStorageConfiguration
        {
            Provider = SyncProvider.S3,
            Enabled = true,
            S3 = new S3StorageConfiguration
            {
                Bucket = "bucket",
                AccessKey = "access",
                SecretKey = "secret",
            },
            Folder = new FolderStorageConfiguration { Path = string.Empty },
        };
        Assert.True(s3.IsConfigured);

        var webDav = new SyncStorageConfiguration
        {
            Provider = SyncProvider.WebDav,
            Enabled = true,
            WebDav = new WebDavStorageConfiguration { Url = "https://webdav.cloud.mail.ru" },
        };
        Assert.True(webDav.IsConfigured);

        var folder = new SyncStorageConfiguration
        {
            Provider = SyncProvider.Folder,
            Enabled = true,
            Folder = new FolderStorageConfiguration { Path = _root },
        };
        Assert.True(folder.IsConfigured);

        var disabled = new SyncStorageConfiguration
        {
            Provider = SyncProvider.Folder,
            Enabled = false,
            Folder = new FolderStorageConfiguration { Path = _root },
        };
        Assert.False(disabled.IsConfigured);
    }

    [Fact]
    public async Task ConnectionTester_WritesAndRemovesProbe()
    {
        var database = new DatabaseConfiguration
        {
            SyncId = Guid.NewGuid(),
            Name = "Folder",
            Path = Path.Combine(_root, "db.db"),
            BackupsFolder = "Backups",
            Sync = new SyncStorageConfiguration
            {
                Provider = SyncProvider.Folder,
                Enabled = true,
                Folder = new FolderStorageConfiguration { Path = _root },
            },
        };

        var tester = new SyncConnectionTester(
            new SyncObjectStoreFactory(NullLoggerFactory.Instance),
            NullLogger<SyncConnectionTester>.Instance);

        var result = await tester.TestAsync(database);

        Assert.True(result.Success, result.Message);
        Assert.Empty(Directory.GetFiles(_root, "*.connection-test", SearchOption.AllDirectories));
    }

    [Fact]
    public void EncryptionViewModel_DerivesKeyAndRejectsWrongPassword()
    {
        var viewModel = new SyncEncryptionViewModel
        {
            Enabled = true,
            Password = "family-passphrase",
        };

        Assert.True(viewModel.EnsureReady());
        Assert.False(string.IsNullOrWhiteSpace(viewModel.Key));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.Salt));
        Assert.True(SyncCrypto.VerifyKeyCheck(
            Convert.FromBase64String(viewModel.Key),
            viewModel.KeyCheck));

        viewModel.Password = "wrong-passphrase";
        Assert.False(viewModel.EnsureReady());
        Assert.Contains("не подходит", viewModel.Status);
    }

    [Fact]
    public void DatabaseViewModel_EncryptionNeedsPasswordOrKey()
    {
        var database = new DatabaseConfiguration
        {
            SyncId = Guid.NewGuid(),
            Name = "Encrypted",
            Path = "database.db",
            BackupsFolder = "Backups",
            Sync = new SyncStorageConfiguration
            {
                Provider = SyncProvider.Folder,
                Enabled = true,
                Folder = new FolderStorageConfiguration { Path = _root },
                Encryption = new SyncEncryptionConfiguration
                {
                    Enabled = true,
                },
            },
        };

        var viewModel = new DatabaseViewModel();
        viewModel.FillFrom(database);

        Assert.False(viewModel.Sync.IsValid);

        viewModel.Sync.Encryption.Password = "family-passphrase";
        Assert.True(viewModel.Sync.IsValid);
        Assert.True(viewModel.Sync.EnsureEncryptionReady());

        var restored = viewModel.ToConfiguration();
        Assert.True(restored.Sync.Encryption.Enabled);
        Assert.False(string.IsNullOrWhiteSpace(restored.Sync.Encryption.Key));
        Assert.True(restored.Sync.HasSettings);
    }

    [Fact]
    public void DatabaseViewModel_RoundTripsProviderSettings()
    {
        var database = new DatabaseConfiguration
        {
            SyncId = Guid.NewGuid(),
            Name = "WebDAV",
            Path = "database.db",
            BackupsFolder = "Backups",
            Sync = new SyncStorageConfiguration
            {
                Provider = SyncProvider.WebDav,
                Enabled = true,
                WebDav = new WebDavStorageConfiguration
                {
                    Url = "https://webdav.cloud.mail.ru",
                    UserName = "family@example.com",
                    Password = "password",
                },
                PublishPeerChangesToCloud = true,
            },
        };

        var viewModel = new DatabaseViewModel();
        viewModel.FillFrom(database);

        var restored = viewModel.ToConfiguration();

        Assert.Equal(SyncProvider.WebDav, restored.Sync.Provider);
        Assert.True(restored.Sync.Enabled);
        Assert.True(restored.Sync.PublishPeerChangesToCloud);
        Assert.Equal("https://webdav.cloud.mail.ru", restored.Sync.WebDav.Url);
        Assert.Equal("family@example.com", restored.Sync.WebDav.UserName);
        Assert.Equal("password", restored.Sync.WebDav.Password);
        Assert.False(restored.S3.Enabled);
    }

    [Fact]
    public void Factory_SelectsStoreByProvider()
    {
        var factory = new SyncObjectStoreFactory(NullLoggerFactory.Instance);
        var s3Configuration = new SyncStorageConfiguration
        {
            Provider = SyncProvider.S3,
            S3 = new S3StorageConfiguration
            {
                Bucket = "bucket",
                AccessKey = "access",
                SecretKey = "secret",
            },
        };
        var webDavConfiguration = new SyncStorageConfiguration
        {
            Provider = SyncProvider.WebDav,
            WebDav = new WebDavStorageConfiguration { Url = "https://webdav.cloud.mail.ru" },
        };
        var folderConfiguration = new SyncStorageConfiguration
        {
            Provider = SyncProvider.Folder,
            Folder = new FolderStorageConfiguration { Path = _root },
        };

        Assert.IsType<S3SyncObjectStore>(factory.Create(s3Configuration));
        Assert.IsType<WebDavSyncObjectStore>(factory.Create(webDavConfiguration));
        Assert.IsType<FolderSyncObjectStore>(factory.Create(folderConfiguration));

        Assert.Throws<InvalidOperationException>(() =>
            factory.Create(new SyncStorageConfiguration { Provider = SyncProvider.None }));
    }
}
