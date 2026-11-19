using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Models;
using FamilyMoney.Models.Sync;
using FamilyMoney.State;
using FamilyMoney.Sync;
using FamilyMoney.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace FamilyMoney.Tests;

public sealed class SyncServiceTests : IDisposable
{
    private static readonly Guid SyncId = Guid.Parse("9d1f6a4e-2b7c-4c1d-9e8f-0a1b2c3d4e5f");

    private static int _outboxId;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "FamilyMoneySyncTests",
        Guid.NewGuid().ToString("N"));

    public SyncServiceTests()
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
    public async Task Push_WritesDeviceJournalAndSnapshot()
    {
        var store = new FakeSyncStore();
        var device = CreateDevice(store, "a");
        var account = NewAccount("РќР°Р»РёС‡РЅС‹Рµ");
        device.Repository.Entities.Add(ToOutbox(account));

        var result = await device.Service.SyncAsync();

        Assert.True(result.Success, result.Message);

        var manifest = ReadDeviceManifest(store, device.Database, device.StateStore.DeviceId);
        Assert.Equal(3, manifest.ProtocolVersion);
        Assert.Equal(device.StateStore.DeviceId, manifest.DeviceId);
        Assert.Equal(1, manifest.LastSequence);

        Assert.True(await store.ExistsAsync(
            SyncPaths.DeviceDeltaKey(device.Database, device.StateStore.DeviceId, 1)));
        Assert.True(await HasSnapshotAsync(store, device.Database));
        Assert.True(device.Repository.CheckpointCount > 0);
        Assert.Empty(device.Repository.Entities);

        var state = LoadState(device);
        Assert.Equal(1, state.AppliedByDevice[device.StateStore.DeviceId]);
        Assert.True(state.LegacyMigrated);
    }

    [Fact]
    public async Task Pull_AppliesRemoteDeviceJournal()
    {
        var store = new FakeSyncStore();
        var device = CreateDevice(store, "b");
        var account = NewAccount("РљР°СЂС‚Р°");
        await SeedDeviceJournalAsync(store, device.Database, "remote", 1, Delta("remote", 1, account));

        var result = await device.Service.SyncAsync();

        Assert.True(result.Success, result.Message);
        Assert.True(device.Repository.Applied.ContainsKey(account.Id));
        Assert.Equal(1, LoadState(device).AppliedByDevice["remote"]);
    }

    [Fact]
    public async Task Pull_AppliesRemoteDeviceJournal_WhenListingKeysHaveLeadingSlash()
    {
        // FluentStorage поверх S3 возвращает ключи с ведущим «/». Раньше из-за
        // этого манифесты устройств не находились по префиксу и чужие дельты
        // молча не применялись: устройство показывало данные старой ревизии.
        var store = new FakeSyncStore { ListKeysWithLeadingSlash = true };
        var device = CreateDevice(store, "slash");
        var account = NewAccount("Key with leading slash");
        await SeedDeviceJournalAsync(store, device.Database, "remote", 1, Delta("remote", 1, account));

        var result = await device.Service.SyncAsync();

        Assert.True(result.Success, result.Message);
        Assert.True(device.Repository.Applied.ContainsKey(account.Id));
        Assert.Equal(1, LoadState(device).AppliedByDevice["remote"]);
    }

    [Fact]
    public async Task LegacyJournal_IsMigratedAndApplied()
    {
        var store = new FakeSyncStore();
        var device = CreateDevice(store, "legacy");
        var account = NewAccount("Из старого журнала");
        await SeedLegacyAsync(store, device.Database, 1, Delta("legacy", 1, account));

        var result = await device.Service.SyncAsync();

        Assert.True(result.Success, result.Message);
        Assert.True(device.Repository.Applied.ContainsKey(account.Id));
        Assert.True(LoadState(device).LegacyMigrated);
        Assert.True(await HasSnapshotAsync(store, device.Database));
    }

    [Fact]
    public async Task Push_ReplayAfterOutboxClearFailure_DoesNotCreateSequence()
    {
        var store = new FakeSyncStore();
        var device = CreateDevice(store, "d");
        var account = NewAccount("Повтор");
        device.Repository.Entities.Add(ToOutbox(account));
        device.Repository.ThrowOnClearOnce = true;

        var first = await device.Service.SyncAsync();
        Assert.False(first.Success);

        var second = await device.Service.SyncAsync();
        Assert.True(second.Success, second.Message);

        var manifest = ReadDeviceManifest(store, device.Database, device.StateStore.DeviceId);
        Assert.Equal(1, manifest.LastSequence);
        Assert.Empty(device.Repository.Entities);
        Assert.Equal(1, LoadState(device).AppliedByDevice[device.StateStore.DeviceId]);
    }

    [Fact]
    public async Task Pull_WhenImageDownloadFails_DoesNotAdvanceDeviceSequence()
    {
        var store = new FakeSyncStore();
        var device = CreateDevice(store, "e");
        var account = NewAccount("С чеком");
        var delta = Delta("remote", 1, account);
        delta.Images.Add(new SyncImageRecord
        {
            EntityId = account.Id,
            LastChange = account.LastChange,
            FileName = "receipt.jpg",
        });

        await SeedDeviceJournalAsync(store, device.Database, "remote", 1, delta);
        store.SetImage(SyncPaths.ImageKey(device.Database, account.Id), [1, 2, 3]);
        device.Repository.FailImageDownloads = true;

        var first = await device.Service.SyncAsync();
        Assert.False(first.Success);
        Assert.Equal(0, LoadState(device).AppliedByDevice.GetValueOrDefault("remote"));

        device.Repository.FailImageDownloads = false;
        var second = await device.Service.SyncAsync();

        Assert.True(second.Success, second.Message);
        Assert.Equal(1, LoadState(device).AppliedByDevice["remote"]);
        Assert.True(device.Repository.Images.ContainsKey(account.Id));
    }

    [Fact]
    public async Task InitialSnapshot_CheckpointsBeforeUpload()
    {
        var store = new FakeSyncStore();
        var device = CreateDevice(store, "f");

        var result = await device.Service.SyncAsync();

        Assert.True(result.Success, result.Message);
        Assert.True(device.Repository.CheckpointCount > 0);
        Assert.True(await HasSnapshotAsync(store, device.Database));
    }

    [Fact]
    public async Task SecondDevice_RestoresRealDatabaseFromSnapshot()
    {
        var store = new FakeSyncStore();
        var source = CreateRealDevice(store, "real-a");
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "РќР°Р»РёС‡РЅС‹Рµ",
        };
        source.Repository.UpdateAccount(account);

        var first = await source.Service.SyncAsync();
        Assert.True(first.Success, first.Message);

        var target = CreateRealDevice(store, "real-b");
        var second = await target.Service.SyncAsync();

        Assert.True(second.Success, second.Message);
        Assert.Contains(target.Repository.GetAccounts(), item => item.Id == account.Id);
    }

    [Fact]
    public async Task FolderProvider_SyncsRealDatabaseBetweenDevices()
    {
        var syncFolder = Path.Combine(_root, "shared-folder");
        Directory.CreateDirectory(syncFolder);

        var source = CreateRealFolderDevice("folder-a", syncFolder);
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Из папки",
        };
        source.Repository.UpdateAccount(account);

        var first = await source.Service.SyncAsync();
        Assert.True(first.Success, first.Message);

        var target = CreateRealFolderDevice("folder-b", syncFolder);
        var second = await target.Service.SyncAsync();

        Assert.True(second.Success, second.Message);
        Assert.Contains(target.Repository.GetAccounts(), item => item.Id == account.Id);
    }

    [Fact]
    public async Task EncryptedSync_RestoresRealDatabaseAndRejectsWrongKey()
    {
        var store = new FakeSyncStore();
        var (salt, key, check) = SyncCrypto.CreateKey("family-passphrase");
        var encryption = new SyncEncryptionConfiguration
        {
            Enabled = true,
            Salt = salt,
            Key = key,
            KeyCheck = check,
        };

        var source = CreateRealDevice(store, "enc-a", encryption);
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Зашифровано",
        };
        source.Repository.UpdateAccount(account);

        var first = await source.Service.SyncAsync();
        Assert.True(first.Success, first.Message);

        var manifestJson = store.GetText(SyncPaths.DeviceManifestKey(
            source.Database,
            source.StateStore.DeviceId));
        Assert.NotNull(manifestJson);
        Assert.StartsWith(SyncCrypto.TextPrefix, manifestJson);

        var target = CreateRealDevice(store, "enc-b", encryption);
        var second = await target.Service.SyncAsync();
        Assert.True(second.Success, second.Message);
        Assert.Contains(target.Repository.GetAccounts(), item => item.Id == account.Id);

        var (_, wrongKey, _) = SyncCrypto.CreateKey("wrong-passphrase");
        var wrongEncryption = new SyncEncryptionConfiguration
        {
            Enabled = true,
            Salt = salt,
            Key = wrongKey,
            KeyCheck = check,
        };
        var wrong = CreateRealDevice(store, "enc-c", wrongEncryption);
        var third = await wrong.Service.SyncAsync();

        Assert.False(third.Success);
        Assert.Contains("подходит", third.Message);
    }

    [Fact]
    public async Task Pull_RestoresSnapshotForNewDevice()
    {
        var store = new FakeSyncStore();
        var device = CreateDevice(store, "k");
        await SeedSnapshotAsync(
            store,
            device.Database,
            "0001",
            "snapshot"u8.ToArray(),
            new Dictionary<string, long>());

        var result = await device.Service.SyncAsync();

        Assert.True(result.Success, result.Message);
        Assert.Equal("snapshot", File.ReadAllText(device.Database.GetResolvedPath()));
    }

    [Fact]
    public async Task SnapshotVector_PullsOnlyJournalTail()
    {
        var store = new FakeSyncStore();
        var device = CreateDevice(store, "tail");
        await SeedSnapshotAsync(
            store,
            device.Database,
            "0001",
            "snapshot"u8.ToArray(),
            new Dictionary<string, long> { ["remote"] = 1 });

        var account = NewAccount("Хвост журнала");
        await SeedDeviceJournalAsync(
            store,
            device.Database,
            "remote",
            2,
            Delta("remote", 2, account));

        var result = await device.Service.SyncAsync();

        Assert.True(result.Success, result.Message);
        Assert.Equal("snapshot", File.ReadAllText(device.Database.GetResolvedPath()));
        Assert.True(device.Repository.Applied.ContainsKey(account.Id));
        Assert.Equal(2, LoadState(device).AppliedByDevice["remote"]);
    }

    [Fact]
    public async Task Pull_WithPendingChanges_DoesNotReplaceDatabaseWithSnapshot()
    {
        var store = new FakeSyncStore();
        var device = CreateDevice(store, "l");
        await SeedSnapshotAsync(
            store,
            device.Database,
            "0001",
            "snapshot"u8.ToArray(),
            new Dictionary<string, long>());

        device.Repository.Entities.Add(ToOutbox(NewAccount("Локальное")));

        var result = await device.Service.SyncAsync();

        Assert.True(result.Success, result.Message);
        Assert.Equal("db", File.ReadAllText(device.Database.GetResolvedPath()));
        Assert.Equal(1, ReadDeviceManifest(
            store,
            device.Database,
            device.StateStore.DeviceId).LastSequence);
    }

    [Fact]
    public async Task TwoDevices_ParallelPush_ConvergeWithoutLosingChanges()
    {
        var store = new FakeSyncStore();
        var first = CreateDevice(store, "p1");
        var second = CreateDevice(store, "p2");

        var firstAccount = NewAccount("РџРµСЂРІС‹Р№");
        var secondAccount = NewAccount("Второй");
        first.Repository.Entities.Add(ToOutbox(firstAccount));
        second.Repository.Entities.Add(ToOutbox(secondAccount));

        await Task.WhenAll(
            first.Service.SyncAsync(),
            second.Service.SyncAsync());

        await first.Service.SyncAsync();
        await second.Service.SyncAsync();

        Assert.Equal(1, ReadDeviceManifest(store, first.Database, first.StateStore.DeviceId).LastSequence);
        Assert.Equal(1, ReadDeviceManifest(store, second.Database, second.StateStore.DeviceId).LastSequence);

        var allDeltas = string.Concat(
            store.GetText(SyncPaths.DeviceDeltaKey(
                first.Database,
                first.StateStore.DeviceId,
                1)),
            store.GetText(SyncPaths.DeviceDeltaKey(
                first.Database,
                second.StateStore.DeviceId,
                1)));

        Assert.Contains(firstAccount.Id.ToString("D"), allDeltas);
        Assert.Contains(secondAccount.Id.ToString("D"), allDeltas);
        Assert.True(first.Repository.Applied.ContainsKey(secondAccount.Id));
        Assert.True(second.Repository.Applied.ContainsKey(firstAccount.Id));
    }

    [Fact]
    public async Task ThreeDevices_Converge()
    {
        var store = new FakeSyncStore();
        var devices = new[]
        {
            CreateDevice(store, "t1"),
            CreateDevice(store, "t2"),
            CreateDevice(store, "t3"),
        };
        var accounts = devices
            .Select((device, index) =>
            {
                var account = NewAccount($"Устройство {index + 1}");
                device.Repository.Entities.Add(ToOutbox(account));
                return account;
            })
            .ToList();

        foreach (var device in devices)
        {
            var result = await device.Service.SyncAsync();
            Assert.True(result.Success, result.Message);
        }

        foreach (var device in devices)
        {
            var result = await device.Service.SyncAsync();
            Assert.True(result.Success, result.Message);
        }

        for (var index = 0; index < devices.Length; index++)
        {
            for (var other = 0; other < accounts.Count; other++)
            {
                if (index == other)
                {
                    continue;
                }

                Assert.True(
                    devices[index].Repository.Applied.ContainsKey(accounts[other].Id),
                    $"Device {index} did not apply account {other}");
            }
        }
    }

    [Fact]
    public async Task Sync_WithoutConditionalWrites_StillCompletes()
    {
        var store = new FakeSyncStore { ConditionalWriteSupported = false };
        var device = CreateDevice(store, "h");
        device.Repository.Entities.Add(ToOutbox(NewAccount("Без условной записи")));

        var result = await device.Service.SyncAsync();

        Assert.True(result.Success, result.Message);
        Assert.Equal(1, ReadDeviceManifest(
            store,
            device.Database,
            device.StateStore.DeviceId).LastSequence);
    }

    [Fact]
    public async Task Pull_RejectsFutureDeviceProtocolVersion()
    {
        var store = new FakeSyncStore();
        var device = CreateDevice(store, "i");
        var account = NewAccount("Р‘СѓРґСѓС‰РµРµ");
        await SeedDeviceJournalAsync(
            store,
            device.Database,
            "future",
            1,
            Delta("future", 1, account),
            protocolVersion: 4);

        var result = await device.Service.SyncAsync();

        Assert.False(result.Success);
        Assert.Contains("Обновите", result.Message);
    }

    [Fact]
    public async Task LegacySnapshot_IsRestoredForNewDevice()
    {
        var store = new FakeSyncStore();
        var device = CreateDevice(store, "legacy-snapshot");
        await SeedLegacySnapshotAsync(store, device.Database, 1, "legacy"u8.ToArray());

        var result = await device.Service.SyncAsync();

        Assert.True(result.Success, result.Message);
        Assert.Equal("legacy", File.ReadAllText(device.Database.GetResolvedPath()));
        Assert.Equal(1, LoadState(device).LegacyAppliedRevision);
    }

    private Device CreateDevice(FakeSyncStore store, string name)
    {
        var home = Path.Combine(_root, name);
        Directory.CreateDirectory(home);

        var database = new DatabaseConfiguration
        {
            SyncId = SyncId,
            Name = name,
            Path = Path.Combine(_root, name + ".db"),
            BackupsFolder = "Backups",
            MaxBackups = 10,
            Sync = new SyncStorageConfiguration
            {
                Provider = SyncProvider.S3,
                Enabled = true,
                S3 = new S3StorageConfiguration
                {
                    Enabled = true,
                    Bucket = "bucket",
                    AccessKey = "access",
                    SecretKey = "secret",
                },
            },
        };
        File.WriteAllText(database.GetResolvedPath(), "db");

        var repository = new FakeSyncRepository();
        var stateStore = new LocalSyncStateStore(home);
        var service = new SyncService(
            new FakeConfiguration(database),
            repository,
            new FakeStoreFactory(store),
            stateStore,
            new SyncImageSynchronizer(repository, NullLogger<SyncImageSynchronizer>.Instance),
            NullLogger<SyncService>.Instance);

        return new Device(database, repository, stateStore, service);
    }

    private RealDevice CreateRealDevice(
        FakeSyncStore store,
        string name,
        SyncEncryptionConfiguration? encryption = null)
    {
        var home = Path.Combine(_root, name);
        Directory.CreateDirectory(home);

        var database = new DatabaseConfiguration
        {
            SyncId = SyncId,
            Name = name,
            Path = Path.Combine(_root, name + ".db"),
            BackupsFolder = "Backups",
            MaxBackups = 10,
            Sync = new SyncStorageConfiguration
            {
                Provider = SyncProvider.S3,
                Enabled = true,
                S3 = new S3StorageConfiguration
                {
                    Enabled = true,
                    Bucket = "bucket",
                    AccessKey = "access",
                    SecretKey = "secret",
                },
                Encryption = encryption ?? new SyncEncryptionConfiguration(),
            },
        };

        var configuration = new FakeConfiguration(database);
        var repository = new LiteDbRepository(
            configuration,
            new AccountLocalSettingsStore(configuration),
            NullLogger<LiteDbRepository>.Instance);
        repository.UpdateDbSchema();

        var stateStore = new LocalSyncStateStore(home);
        var service = new SyncService(
            configuration,
            repository,
            new FakeStoreFactory(store),
            stateStore,
            new SyncImageSynchronizer(repository, NullLogger<SyncImageSynchronizer>.Instance),
            NullLogger<SyncService>.Instance);

        return new RealDevice(database, repository, stateStore, service);
    }

    private RealDevice CreateRealFolderDevice(string name, string syncFolder)
    {
        var home = Path.Combine(_root, name);
        Directory.CreateDirectory(home);

        var database = new DatabaseConfiguration
        {
            SyncId = SyncId,
            Name = name,
            Path = Path.Combine(_root, name + ".db"),
            BackupsFolder = "Backups",
            MaxBackups = 10,
            Sync = new SyncStorageConfiguration
            {
                Provider = SyncProvider.Folder,
                Enabled = true,
                Folder = new FolderStorageConfiguration { Path = syncFolder },
            },
        };

        var configuration = new FakeConfiguration(database);
        var repository = new LiteDbRepository(
            configuration,
            new AccountLocalSettingsStore(configuration),
            NullLogger<LiteDbRepository>.Instance);
        repository.UpdateDbSchema();

        var stateStore = new LocalSyncStateStore(home);
        var service = new SyncService(
            configuration,
            repository,
            new SyncObjectStoreFactory(NullLoggerFactory.Instance),
            stateStore,
            new SyncImageSynchronizer(repository, NullLogger<SyncImageSynchronizer>.Instance),
            NullLogger<SyncService>.Instance);

        return new RealDevice(database, repository, stateStore, service);
    }

    private static LocalSyncState LoadState(Device device) =>
        device.StateStore.Load(device.Database.SyncId, device.StateStore.DeviceId);

    private static DeviceSyncManifest ReadDeviceManifest(
        FakeSyncStore store,
        DatabaseConfiguration database,
        string deviceId)
    {
        var json = store.GetText(SyncPaths.DeviceManifestKey(database, deviceId));
        Assert.NotNull(json);
        return JsonSerializer.Deserialize<DeviceSyncManifest>(json!, JsonDefaults.Indented)
            ?? throw new InvalidOperationException("Device manifest is not readable.");
    }

    private static async Task<bool> HasSnapshotAsync(
        FakeSyncStore store,
        DatabaseConfiguration database)
    {
        var objects = await store.ListAsync(SyncPaths.SnapshotsPrefix(database));
        return objects.Any(item => item.Key.EndsWith(".json", StringComparison.Ordinal));
    }

    private static async Task SeedDeviceJournalAsync(
        FakeSyncStore store,
        DatabaseConfiguration database,
        string deviceId,
        long sequence,
        SyncDelta delta,
        int protocolVersion = 3)
    {
        await store.WriteTextUnconditionalAsync(
            SyncPaths.DeviceDeltaKey(database, deviceId, sequence),
            JsonSerializer.Serialize(delta, JsonDefaults.Indented));

        var manifest = new DeviceSyncManifest
        {
            ProtocolVersion = protocolVersion,
            DeviceId = deviceId,
            LastSequence = sequence,
            UpdatedAt = DateTime.UtcNow,
            LastOperationId = "seed",
        };

        await store.WriteTextUnconditionalAsync(
            SyncPaths.DeviceManifestKey(database, deviceId),
            JsonSerializer.Serialize(manifest, JsonDefaults.Indented));
    }

    private static async Task SeedSnapshotAsync(
        FakeSyncStore store,
        DatabaseConfiguration database,
        string snapshotId,
        byte[] databaseBytes,
        Dictionary<string, long> appliedByDevice)
    {
        var metadata = new SyncSnapshotMetadata
        {
            ProtocolVersion = 3,
            CreatedAt = DateTime.UtcNow,
            DeviceId = "seed",
            AppliedByDevice = appliedByDevice,
        };

        await store.WriteTextUnconditionalAsync(
            SyncPaths.SnapshotMetadataKey(database, snapshotId),
            JsonSerializer.Serialize(metadata, JsonDefaults.Indented));
        store.SetFile(SyncPaths.SnapshotKey(database, snapshotId), databaseBytes);
    }

    private static async Task SeedLegacyAsync(
        FakeSyncStore store,
        DatabaseConfiguration database,
        long revision,
        SyncDelta delta)
    {
        await store.WriteTextUnconditionalAsync(
            SyncPaths.DeltaKey(database, revision),
            JsonSerializer.Serialize(delta, JsonDefaults.Indented));

        var manifest = new SyncManifest
        {
            ProtocolVersion = 2,
            Revision = revision,
            UpdatedAt = DateTime.UtcNow,
            UpdatedByDevice = "legacy",
            OperationId = "legacy",
        };

        await store.WriteTextUnconditionalAsync(
            SyncPaths.ManifestKey(database),
            JsonSerializer.Serialize(manifest, JsonDefaults.Indented));
    }

    private static async Task SeedLegacySnapshotAsync(
        FakeSyncStore store,
        DatabaseConfiguration database,
        long revision,
        byte[] databaseBytes)
    {
        var manifest = new SyncManifest
        {
            ProtocolVersion = 2,
            Revision = revision,
            UpdatedAt = DateTime.UtcNow,
            UpdatedByDevice = "legacy",
            SnapshotRevision = revision,
            SnapshotKey = SyncPaths.SnapshotKey(database, revision),
            OperationId = "legacy",
        };

        await store.WriteTextUnconditionalAsync(
            SyncPaths.ManifestKey(database),
            JsonSerializer.Serialize(manifest, JsonDefaults.Indented));
        store.SetFile(SyncPaths.SnapshotKey(database, revision), databaseBytes);
    }

    private static SyncDelta Delta(string deviceId, long revision, params Account[] accounts)
    {
        var delta = new SyncDelta
        {
            Revision = revision,
            DeviceId = deviceId,
            CreatedAt = DateTime.UtcNow,
        };

        foreach (var account in accounts)
        {
            delta.Accounts.Add(SyncEntitySerializer.CreateRecord(account));
        }

        return delta;
    }

    private static Account NewAccount(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        LastChange = DateTime.UtcNow,
    };

    private static SyncOutboxEntry ToOutbox(Account account)
    {
        var record = SyncEntitySerializer.CreateRecord(account);
        return new SyncOutboxEntry
        {
            Id = Interlocked.Increment(ref _outboxId),
            EntityType = record.EntityType,
            EntityId = record.Id,
            LastChange = record.LastChange,
            DeletedAt = record.DeletedAt,
            DataJson = record.DataJson,
        };
    }

    private sealed record Device(
        DatabaseConfiguration Database,
        FakeSyncRepository Repository,
        LocalSyncStateStore StateStore,
        SyncService Service);

    private sealed record RealDevice(
        DatabaseConfiguration Database,
        LiteDbRepository Repository,
        LocalSyncStateStore StateStore,
        SyncService Service);
}
