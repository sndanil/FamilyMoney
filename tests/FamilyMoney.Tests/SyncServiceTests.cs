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
    public async Task Push_WritesDeltaManifestAndCheckpointedSnapshot()
    {
        var store = new FakeSyncStore();
        var device = CreateDevice(store, "a");
        var account = NewAccount("Наличные");
        device.Repository.Entities.Add(ToOutbox(account));

        var result = await device.Service.SyncAsync();

        Assert.True(result.Success, result.Message);

        var manifest = ReadManifest(store, device.Database);
        Assert.Equal(1, manifest.Revision);
        Assert.Equal(2, manifest.ProtocolVersion);
        Assert.False(string.IsNullOrWhiteSpace(manifest.OperationId));

        Assert.True(await store.ExistsAsync(SyncPaths.DeltaKey(device.Database, 1)));
        Assert.True(await store.ExistsAsync(SyncPaths.SnapshotKey(device.Database, 1)));
        Assert.True(device.Repository.CheckpointCount > 0);
        Assert.Empty(device.Repository.Entities);

        var state = LoadState(device);
        Assert.Equal(1, state.AppliedRevision);
        Assert.Equal(1, state.PublishedRevision);
        Assert.Null(state.PendingOperationId);
    }

    [Fact]
    public async Task Pull_AppliesRemoteDelta()
    {
        var store = new FakeSyncStore();
        var device = CreateDevice(store, "b");
        var account = NewAccount("Карта");
        await SeedAsync(store, device.Database, 1, Delta(1, account));

        var result = await device.Service.SyncAsync();

        Assert.True(result.Success, result.Message);
        Assert.True(device.Repository.Applied.ContainsKey(account.Id));
        Assert.Equal(1, LoadState(device).AppliedRevision);
    }

    [Fact]
    public async Task Push_AfterManifestConflict_RetriesAndKeepsBothChanges()
    {
        var store = new FakeSyncStore();
        var device = CreateDevice(store, "c");

        var seededAccount = NewAccount("Старый");
        await SeedAsync(store, device.Database, 1, Delta(1, seededAccount));

        var localAccount = NewAccount("Новый");
        device.Repository.Entities.Add(ToOutbox(localAccount));

        var otherAccount = NewAccount("Чужой");
        var otherDelta = Delta(2, otherAccount);
        var otherManifest = new SyncManifest
        {
            ProtocolVersion = 2,
            Revision = 2,
            UpdatedAt = DateTime.UtcNow,
            UpdatedByDevice = "other",
            OperationId = "other",
        };

        var conflictTriggered = 0;
        store.BeforeConditionalWrite = async key =>
        {
            if (!key.EndsWith("/manifest.json", StringComparison.Ordinal))
            {
                return;
            }

            if (Interlocked.Exchange(ref conflictTriggered, 1) != 0)
            {
                return;
            }

            await store.WriteTextUnconditionalAsync(
                SyncPaths.DeltaKey(device.Database, 2),
                JsonSerializer.Serialize(otherDelta, JsonDefaults.Indented));
            await store.WriteTextUnconditionalAsync(
                SyncPaths.ManifestKey(device.Database),
                JsonSerializer.Serialize(otherManifest, JsonDefaults.Indented));

            throw new SyncConflictException("simulated concurrent write");
        };

        var result = await device.Service.SyncAsync();

        Assert.True(result.Success, result.Message);
        Assert.True(device.Repository.Applied.ContainsKey(seededAccount.Id));
        Assert.True(device.Repository.Applied.ContainsKey(otherAccount.Id));

        var manifest = ReadManifest(store, device.Database);
        Assert.Equal(3, manifest.Revision);

        var delta3 = store.GetText(SyncPaths.DeltaKey(device.Database, 3));
        Assert.NotNull(delta3);
        Assert.Contains(localAccount.Id.ToString("D"), delta3);
    }

    [Fact]
    public async Task Push_ReplayAfterOutboxClearFailure_DoesNotCreateRevision()
    {
        var store = new FakeSyncStore();
        var device = CreateDevice(store, "d");
        var account = NewAccount("Повтор");
        device.Repository.Entities.Add(ToOutbox(account));
        device.Repository.ThrowOnClearOnce = true;

        var first = await device.Service.SyncAsync();

        Assert.False(first.Success);
        Assert.Equal(1, ReadManifest(store, device.Database).Revision);
        Assert.NotEmpty(device.Repository.Entities);

        var second = await device.Service.SyncAsync();

        Assert.True(second.Success, second.Message);
        Assert.Equal(1, ReadManifest(store, device.Database).Revision);
        Assert.Empty(device.Repository.Entities);
        Assert.Equal(1, LoadState(device).AppliedRevision);
    }

    [Fact]
    public async Task Pull_WhenImageDownloadFails_DoesNotAdvanceRevision()
    {
        var store = new FakeSyncStore();
        var device = CreateDevice(store, "e");
        var account = NewAccount("С чеком");
        var delta = Delta(1, account);
        delta.Images.Add(new SyncImageRecord
        {
            EntityId = account.Id,
            LastChange = account.LastChange,
            FileName = "receipt.jpg",
        });

        await SeedAsync(store, device.Database, 1, delta);
        store.SetImage(SyncPaths.ImageKey(device.Database, account.Id), [1, 2, 3]);
        device.Repository.FailImageDownloads = true;

        var first = await device.Service.SyncAsync();

        Assert.False(first.Success);
        Assert.Contains("изображ", first.Message);
        Assert.Equal(0, LoadState(device).AppliedRevision);

        device.Repository.FailImageDownloads = false;
        var second = await device.Service.SyncAsync();

        Assert.True(second.Success, second.Message);
        Assert.Equal(1, LoadState(device).AppliedRevision);
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
        Assert.True(await store.ExistsAsync(SyncPaths.SnapshotKey(device.Database, 1)));
        Assert.Equal(1, ReadManifest(store, device.Database).SnapshotRevision);
    }

    [Fact]
    public async Task SecondDevice_RestoresRealDatabaseFromSnapshot()
    {
        var store = new FakeSyncStore();
        var source = CreateRealDevice(store, "real-a");
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Наличные",
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
    public async Task Pull_RestoresSnapshotForNewDevice()
    {
        var store = new FakeSyncStore();
        var device = CreateDevice(store, "k");
        var snapshotKey = SyncPaths.SnapshotKey(device.Database, 1);
        store.SetFile(snapshotKey, "snapshot"u8.ToArray());

        await SeedAsync(
            store,
            device.Database,
            1,
            Delta(1, NewAccount("Из снапшота")),
            new SyncManifest
            {
                ProtocolVersion = 2,
                Revision = 1,
                SnapshotRevision = 1,
                SnapshotKey = snapshotKey,
                UpdatedAt = DateTime.UtcNow,
                UpdatedByDevice = "seed",
                OperationId = "seed",
            });

        var result = await device.Service.SyncAsync();

        Assert.True(result.Success, result.Message);
        Assert.Equal("snapshot", File.ReadAllText(device.Database.GetResolvedPath()));
        Assert.Equal(1, LoadState(device).AppliedRevision);
    }

    [Fact]
    public async Task Pull_WithPendingChanges_DoesNotReplaceDatabaseWithSnapshot()
    {
        var store = new FakeSyncStore();
        var device = CreateDevice(store, "l");
        var snapshotKey = SyncPaths.SnapshotKey(device.Database, 1);
        store.SetFile(snapshotKey, "snapshot"u8.ToArray());

        await SeedAsync(
            store,
            device.Database,
            1,
            Delta(1, NewAccount("Из облака")),
            new SyncManifest
            {
                ProtocolVersion = 2,
                Revision = 1,
                SnapshotRevision = 1,
                SnapshotKey = snapshotKey,
                UpdatedAt = DateTime.UtcNow,
                UpdatedByDevice = "seed",
                OperationId = "seed",
            });

        device.Repository.Entities.Add(ToOutbox(NewAccount("Локальное")));

        var result = await device.Service.SyncAsync();

        Assert.True(result.Success, result.Message);
        Assert.Equal("db", File.ReadAllText(device.Database.GetResolvedPath()));
        Assert.Equal(2, ReadManifest(store, device.Database).Revision);
    }

    [Fact]
    public async Task TwoDevices_ParallelPush_ConvergeWithoutLosingChanges()
    {
        var store = new FakeSyncStore();
        var first = CreateDevice(store, "p1");
        var second = CreateDevice(store, "p2");

        var firstAccount = NewAccount("Первый");
        var secondAccount = NewAccount("Второй");
        first.Repository.Entities.Add(ToOutbox(firstAccount));
        second.Repository.Entities.Add(ToOutbox(secondAccount));

        await Task.WhenAll(
            first.Service.SyncAsync(),
            second.Service.SyncAsync());

        // Одно из устройств могло быть пропущено из-за блокировки — даём
        // ему завершить синхронизацию.
        await first.Service.SyncAsync();
        await second.Service.SyncAsync();

        var manifest = ReadManifest(store, first.Database);
        Assert.Equal(2, manifest.Revision);

        var allDeltas = string.Concat(
            store.GetText(SyncPaths.DeltaKey(first.Database, 1)),
            store.GetText(SyncPaths.DeltaKey(first.Database, 2)));

        Assert.Contains(firstAccount.Id.ToString("D"), allDeltas);
        Assert.Contains(secondAccount.Id.ToString("D"), allDeltas);
    }

    [Fact]
    public async Task Sync_WhenAnotherDeviceHoldsLock_IsSkipped()
    {
        var store = new FakeSyncStore();
        var device = CreateDevice(store, "g");

        var heldLock = await SyncLock.TryAcquireAsync(
            device.Database,
            store,
            "other-device",
            "other-operation");
        Assert.NotNull(heldLock);

        var skipped = await device.Service.SyncAsync();

        Assert.True(skipped.Success);
        Assert.Contains("другом устройстве", skipped.Message);

        await heldLock!.DisposeAsync();

        var completed = await device.Service.SyncAsync();

        Assert.True(completed.Success, completed.Message);
        Assert.Equal(1, ReadManifest(store, device.Database).Revision);
    }

    [Fact]
    public async Task Sync_TakesOverLockLeftBySameDevice()
    {
        var store = new FakeSyncStore();
        var device = CreateDevice(store, "m");

        var staleLock = await SyncLock.TryAcquireAsync(
            device.Database,
            store,
            device.StateStore.DeviceId,
            "stale-operation");
        Assert.NotNull(staleLock);

        var result = await device.Service.SyncAsync();

        Assert.True(result.Success, result.Message);
        Assert.DoesNotContain("другом устройстве", result.Message);

        await staleLock!.DisposeAsync();
    }

    [Fact]
    public async Task Sync_WithoutConditionalWrites_StillCompletes()
    {
        var store = new FakeSyncStore { ConditionalWriteSupported = false };
        var device = CreateDevice(store, "h");
        device.Repository.Entities.Add(ToOutbox(NewAccount("Без условной записи")));

        var result = await device.Service.SyncAsync();

        Assert.True(result.Success, result.Message);
        Assert.Equal(1, ReadManifest(store, device.Database).Revision);
    }

    [Fact]
    public async Task Pull_RejectsFutureProtocolVersion()
    {
        var store = new FakeSyncStore();
        var device = CreateDevice(store, "i");
        var account = NewAccount("Будущее");
        await SeedAsync(
            store,
            device.Database,
            1,
            Delta(1, account),
            new SyncManifest
            {
                ProtocolVersion = 3,
                Revision = 1,
                UpdatedAt = DateTime.UtcNow,
                UpdatedByDevice = "future",
                OperationId = "future",
            });

        var result = await device.Service.SyncAsync();

        Assert.False(result.Success);
        Assert.Contains("Обновите", result.Message);
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
            S3 = new S3StorageConfiguration
            {
                Enabled = true,
                Bucket = "bucket",
                AccessKey = "access",
                SecretKey = "secret",
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

    private RealDevice CreateRealDevice(FakeSyncStore store, string name)
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
            S3 = new S3StorageConfiguration
            {
                Enabled = true,
                Bucket = "bucket",
                AccessKey = "access",
                SecretKey = "secret",
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

        return new RealDevice(repository, stateStore, service);
    }

    private static LocalSyncState LoadState(Device device) =>
        device.StateStore.Load(device.Database.SyncId, device.StateStore.DeviceId);

    private static SyncManifest ReadManifest(FakeSyncStore store, DatabaseConfiguration database)
    {
        var json = store.GetText(SyncPaths.ManifestKey(database));
        Assert.NotNull(json);
        return JsonSerializer.Deserialize<SyncManifest>(json!, JsonDefaults.Indented)
            ?? throw new InvalidOperationException("Manifest is not readable.");
    }

    private static async Task SeedAsync(
        FakeSyncStore store,
        DatabaseConfiguration database,
        long revision,
        SyncDelta delta,
        SyncManifest? manifest = null)
    {
        await store.WriteTextUnconditionalAsync(
            SyncPaths.DeltaKey(database, revision),
            JsonSerializer.Serialize(delta, JsonDefaults.Indented));

        var actualManifest = manifest ?? new SyncManifest
        {
            ProtocolVersion = 2,
            Revision = revision,
            UpdatedAt = DateTime.UtcNow,
            UpdatedByDevice = "seed",
            OperationId = "seed",
        };

        await store.WriteTextUnconditionalAsync(
            SyncPaths.ManifestKey(database),
            JsonSerializer.Serialize(actualManifest, JsonDefaults.Indented));
    }

    private static SyncDelta Delta(long revision, params Account[] accounts)
    {
        var delta = new SyncDelta
        {
            Revision = revision,
            DeviceId = "seed",
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
        LiteDbRepository Repository,
        LocalSyncStateStore StateStore,
        SyncService Service);

    private sealed class FakeConfiguration : IGlobalConfiguration
    {
        public FakeConfiguration(DatabaseConfiguration database)
        {
            Root = new RootConfiguration
            {
                Databases = [database],
                SelectedDatabaseIndex = 0,
            };
        }

        public RootConfiguration Root { get; private set; }

        public RootConfiguration Get() => Root;

        public DatabaseConfiguration GetSelectedDatabase() =>
            Root.Databases[Root.SelectedDatabaseIndex];

        public void Save(RootConfiguration configuration) => Root = configuration;
    }

    private sealed class FakeStoreFactory : ISyncObjectStoreFactory
    {
        private readonly ISyncObjectStore _store;

        public FakeStoreFactory(ISyncObjectStore store)
        {
            _store = store;
        }

        public ISyncObjectStore Create(S3StorageConfiguration configuration) => _store;
    }

    private sealed class FakeSyncStore : ISyncObjectStore
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, TextEntry> _text = new(StringComparer.Ordinal);
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

        public bool ConditionalWriteSupported { get; set; } = true;

        public bool SupportsConditionalWrite => ConditionalWriteSupported;

        public Func<string, Task>? BeforeConditionalWrite { get; set; }

        public Task<(string? Content, string? ETag)> TryDownloadTextAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                return Task.FromResult(_text.TryGetValue(key, out var entry)
                    ? (entry.Content, entry.ETag)
                    : ((string?)null, (string?)null));
            }
        }

        public async Task UploadTextAsync(
            string key,
            string content,
            SyncWriteCondition condition,
            CancellationToken cancellationToken = default)
        {
            if (condition.IsConditional && BeforeConditionalWrite != null)
            {
                await BeforeConditionalWrite(key);
            }

            lock (_gate)
            {
                if (ConditionalWriteSupported)
                {
                    if (condition.IfNoneMatch && _text.ContainsKey(key))
                    {
                        throw new SyncConflictException("Object already exists.");
                    }

                    if (!string.IsNullOrWhiteSpace(condition.IfMatchEtag))
                    {
                        if (!_text.TryGetValue(key, out var existing)
                            || !string.Equals(existing.ETag, condition.IfMatchEtag, StringComparison.Ordinal))
                        {
                            throw new SyncConflictException("Object was changed.");
                        }
                    }
                }

                _text[key] = new TextEntry(content, Guid.NewGuid().ToString("N"));
            }
        }

        public Task WriteTextUnconditionalAsync(string key, string content)
        {
            lock (_gate)
            {
                _text[key] = new TextEntry(content, Guid.NewGuid().ToString("N"));
            }

            return Task.CompletedTask;
        }

        public Task UploadFileAsync(
            string key,
            string localFilePath,
            CancellationToken cancellationToken = default)
        {
            var bytes = File.ReadAllBytes(localFilePath);
            lock (_gate)
            {
                _files[key] = bytes;
            }

            return Task.CompletedTask;
        }

        public Task DownloadFileAsync(
            string key,
            string localFilePath,
            CancellationToken cancellationToken = default)
        {
            byte[] bytes;
            lock (_gate)
            {
                if (!_files.TryGetValue(key, out var stored))
                {
                    throw new FileNotFoundException("Sync object was not found.", key);
                }

                bytes = stored;
            }

            var directory = Path.GetDirectoryName(localFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(localFilePath, bytes);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                return Task.FromResult(_text.ContainsKey(key) || _files.ContainsKey(key));
            }
        }

        public async Task UploadStreamAsync(
            string key,
            Stream stream,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            lock (_gate)
            {
                _files[key] = memory.ToArray();
            }
        }

        public Task<Stream?> DownloadStreamAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                return Task.FromResult<Stream?>(_files.TryGetValue(key, out var bytes)
                    ? new MemoryStream(bytes)
                    : null);
            }
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _text.Remove(key);
                _files.Remove(key);
            }

            return Task.CompletedTask;
        }

        public string? GetText(string key)
        {
            lock (_gate)
            {
                return _text.TryGetValue(key, out var entry) ? entry.Content : null;
            }
        }

        public void SetImage(string key, byte[] content)
        {
            lock (_gate)
            {
                _files[key] = content;
            }
        }

        public void SetFile(string key, byte[] content)
        {
            lock (_gate)
            {
                _files[key] = content;
            }
        }

        private sealed record TextEntry(string Content, string ETag);
    }

    private sealed class FakeSyncRepository : IRepository
    {
        public List<SyncOutboxEntry> Entities { get; } = [];

        public List<SyncImageOutboxEntry> ImagesOutbox { get; } = [];

        public Dictionary<Guid, SyncEntityRecord> Applied { get; } = [];

        public Dictionary<Guid, byte[]> Images { get; } = [];

        public bool FailImageDownloads { get; set; }

        public bool ThrowOnClearOnce { get; set; }

        public int CheckpointCount { get; private set; }

        public SyncPendingChanges GetPendingSyncChanges() => new()
        {
            Entities = Entities.ToList(),
            Images = ImagesOutbox.ToList(),
        };

        public void ClearPendingSyncChanges(SyncPendingChanges pushed)
        {
            if (ThrowOnClearOnce)
            {
                ThrowOnClearOnce = false;
                throw new InvalidOperationException("Simulated failure after publishing the delta.");
            }

            var entityIds = pushed.Entities.Select(entry => entry.Id).ToHashSet();
            Entities.RemoveAll(entry => entityIds.Contains(entry.Id));

            var imageIds = pushed.Images.Select(image => image.Id).ToHashSet();
            ImagesOutbox.RemoveAll(image => imageIds.Contains(image.Id));
        }

        public void ApplySyncDelta(SyncDelta delta)
        {
            foreach (var record in delta.Accounts
                         .Concat(delta.Categories)
                         .Concat(delta.SubCategories)
                         .Concat(delta.Transactions))
            {
                Applied[record.Id] = record;
            }
        }

        public async Task<SyncImagesApplyResult> ApplySyncedImagesAsync(
            IEnumerable<SyncImageRecord> images,
            Func<SyncImageRecord, CancellationToken, Task<Stream?>> downloadImageAsync,
            CancellationToken cancellationToken = default)
        {
            var failed = new List<Guid>();

            foreach (var image in images)
            {
                if (image.DeletedAt != null)
                {
                    Images.Remove(image.EntityId);
                    continue;
                }

                if (FailImageDownloads)
                {
                    failed.Add(image.EntityId);
                    continue;
                }

                await using var stream = await downloadImageAsync(image, cancellationToken);
                if (stream == null)
                {
                    failed.Add(image.EntityId);
                    continue;
                }

                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory, cancellationToken);
                Images[image.EntityId] = memory.ToArray();
            }

            return new SyncImagesApplyResult(failed);
        }

        public void Checkpoint() => CheckpointCount++;

        public void UpdateDbSchema()
        {
        }

        public void DoBackup()
        {
        }

        public void DoBackup(DatabaseConfiguration configuration)
        {
        }

        public DatabaseBackupResult BackupNow(DatabaseConfiguration configuration) =>
            new(false, "not supported");

        public DatabaseBackupResult BackupToFile(DatabaseConfiguration configuration, string destinationPath) =>
            new(false, "not supported");

        public DatabaseBackupResult RestoreFromFile(DatabaseConfiguration configuration, string backupPath) =>
            new(false, "not supported");

        public void UpdateImage(Guid id, string fileName, Stream stream)
        {
        }

        public Stream? TryGetImage(Guid id) => null;

        public Account GetAccount(Guid id) => throw new NotImplementedException();

        public IEnumerable<Account> GetAccounts() => [];

        public void UpdateAccount(Account account) => throw new NotImplementedException();

        public void DeleteAccount(Guid id) => throw new NotImplementedException();

        public void RecalculateAccountBalances()
        {
        }

        public Category GetCategory(Guid id) => throw new NotImplementedException();

        public IEnumerable<Category> GetCategories() => [];

        public void UpdateCategroty(Category category) => throw new NotImplementedException();

        public SubCategory GetSubCategory(Guid id) => throw new NotImplementedException();

        public SubCategory GetOrCreateSubCategory(
            Guid? categoryId,
            string name,
            Func<SubCategory> factory) => throw new NotImplementedException();

        public IEnumerable<SubCategory> GetSubCategories() => [];

        public void UpdateSubCategory(SubCategory subCategory) => throw new NotImplementedException();

        public IReadOnlyDictionary<Guid, int> CountActiveTransactionsBySubCategories(
            IEnumerable<Guid> subCategoryIds) => new Dictionary<Guid, int>();

        public void MergeSubCategories(Guid targetId, IReadOnlyCollection<Guid> sourceIds) =>
            throw new NotImplementedException();

        public IEnumerable<SubCategoryLastSum> GetLastSumsBySubCategories(
            DateTime from,
            IEnumerable<Guid> subCategoryIds) => [];

        public IEnumerable<SubCategoryCommentSuggestions> GetCommentSuggestionsBySubCategories(
            DateTime from) => [];

        public IEnumerable<SubCategoryTags> GetTagsBySubCategories(DateTime from) => [];

        public IEnumerable<Transaction> GetTransactions(TransactionsFilter filter) => [];

        public Transaction? GetTransaction(Guid id) => null;

        public void DeleteTransaction(Guid id) => throw new NotImplementedException();

        public void UpdateTransaction(Transaction transaction) => throw new NotImplementedException();

        public void InsertTransactions(IEnumerable<Transaction> transactions) =>
            throw new NotImplementedException();
    }
}
