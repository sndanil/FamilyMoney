using FamilyMoney.Configuration;
using FamilyMoney.Sync;
using FamilyMoney.ViewModels.Settings;
using System.Text;
using Xunit;

namespace FamilyMoney.Tests;

public sealed class SyncEncryptionTests
{
    [Fact]
    public void PasswordDerivation_RoundTripsAndRejectsWrongKey()
    {
        var (salt, key, check) = SyncCrypto.CreateKey("family-secret");
        var derived = SyncCrypto.DeriveKey(
            "family-secret",
            Convert.FromBase64String(salt));

        Assert.Equal(key, Convert.ToBase64String(derived));
        Assert.True(SyncCrypto.VerifyKeyCheck(derived, check));
        Assert.False(SyncCrypto.VerifyKeyCheck(
            SyncCrypto.DeriveKey("other-secret", Convert.FromBase64String(salt)),
            check));
    }

    [Fact]
    public void Crypto_EncryptsTextAndKeepsLegacyPlaintext()
    {
        var (salt, key, check) = SyncCrypto.CreateKey("secret");
        var crypto = SyncCrypto.Create(new SyncEncryptionConfiguration
        {
            Enabled = true,
            Salt = salt,
            Key = key,
            KeyCheck = check,
        })!;

        var encrypted = crypto.EncryptText("{\"name\":\"Семья\"}", "manifest.json");
        Assert.StartsWith(SyncCrypto.TextPrefix, encrypted);
        Assert.DoesNotContain("Семья", encrypted);
        Assert.Equal("{\"name\":\"Семья\"}", crypto.DecryptText(encrypted, "manifest.json"));
        Assert.Equal("legacy", crypto.DecryptText("legacy", "manifest.json"));
    }

    [Fact]
    public void RecoveryCode_RestoresTheKey()
    {
        var (salt, key, check) = SyncCrypto.CreateKey("secret");
        var viewModel = new SyncEncryptionViewModel
        {
            Enabled = true,
            Salt = salt,
            KeyCheck = check,
            RecoveryCode = key,
        };

        Assert.True(viewModel.EnsureReady());
        Assert.Equal(key, viewModel.Key);
    }

    [Fact]
    public async Task EncryptedStore_RoundTripsTextFileAndStream()
    {
        var inner = new FakeSyncStore();
        var (salt, key, check) = SyncCrypto.CreateKey("secret");
        var crypto = SyncCrypto.Create(new SyncEncryptionConfiguration
        {
            Enabled = true,
            Salt = salt,
            Key = key,
            KeyCheck = check,
        })!;
        var store = new EncryptedSyncObjectStore(inner, crypto);

        await store.UploadTextAsync("familymoney/manifest.json", "secret text", SyncWriteCondition.None);
        var rawText = inner.GetText("familymoney/manifest.json");
        Assert.NotNull(rawText);
        Assert.StartsWith(SyncCrypto.TextPrefix, rawText);
        Assert.DoesNotContain("secret text", rawText);
        Assert.Equal("secret text", (await store.TryDownloadTextAsync("familymoney/manifest.json")).Content);

        var source = Path.Combine(Path.GetTempPath(), "familymoney-enc-" + Guid.NewGuid().ToString("N"));
        var restored = source + ".out";
        try
        {
            await File.WriteAllBytesAsync(source, [1, 2, 3, 4]);
            await store.UploadFileAsync("familymoney/snapshot.db", source);
            await store.DownloadFileAsync("familymoney/snapshot.db", restored);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(restored));
        }
        finally
        {
            if (File.Exists(source))
            {
                File.Delete(source);
            }

            if (File.Exists(restored))
            {
                File.Delete(restored);
            }
        }

        await using var image = new MemoryStream([5, 6, 7]);
        await store.UploadStreamAsync("familymoney/images/a", image, "a.jpg");
        await using var downloaded = await store.DownloadStreamAsync("familymoney/images/a");
        Assert.NotNull(downloaded);
        using var memory = new MemoryStream();
        await downloaded!.CopyToAsync(memory);
        Assert.Equal(new byte[] { 5, 6, 7 }, memory.ToArray());
    }

    [Fact]
    public void QrPayload_HidesEncryptionKeyByDefault()
    {
        var (salt, key, check) = SyncCrypto.CreateKey("secret");
        var database = new DatabaseConfiguration
        {
            SyncId = Guid.NewGuid(),
            Name = "Encrypted",
            Sync = new SyncStorageConfiguration
            {
                Provider = SyncProvider.Folder,
                Enabled = true,
                Folder = new FolderStorageConfiguration { Path = "/tmp/sync" },
                Encryption = new SyncEncryptionConfiguration
                {
                    Enabled = true,
                    Salt = salt,
                    Key = key,
                    KeyCheck = check,
                },
            },
        };

        var safeJson = DatabaseQrPayload.FromConfiguration(database).ToJson();
        Assert.DoesNotContain(key, safeJson);
        Assert.Contains(salt, safeJson);

        var unsafeJson = DatabaseQrPayload.FromConfiguration(database, includeSecrets: true).ToJson();
        Assert.Contains(key, unsafeJson);
    }
}
