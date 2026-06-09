using FamilyMoney.Configuration;
using FamilyMoney.Messages;
using CommunityToolkit.Mvvm.Messaging;
using LiteDB;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace FamilyMoney.Sync;

public sealed class SyncService : ISyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IGlobalConfiguration _configuration;
    private readonly ISyncObjectStoreFactory _objectStoreFactory;
    private readonly LocalSyncStateStore _stateStore;
    private readonly LiteDbSyncOutbox _outbox;
    private readonly LiteDbSyncImageOutbox _imageOutbox;
    private readonly SyncDeltaApplier _applier;
    private readonly SyncImageSynchronizer _imageSynchronizer;
    private readonly ILogger<SyncService> _logger;
    private readonly string _deviceId;

    public SyncService(
        IGlobalConfiguration configuration,
        ISyncObjectStoreFactory objectStoreFactory,
        LocalSyncStateStore stateStore,
        LiteDbSyncOutbox outbox,
        LiteDbSyncImageOutbox imageOutbox,
        SyncDeltaApplier applier,
        SyncImageSynchronizer imageSynchronizer,
        ILogger<SyncService> logger)
    {
        _configuration = configuration;
        _objectStoreFactory = objectStoreFactory;
        _stateStore = stateStore;
        _outbox = outbox;
        _imageOutbox = imageOutbox;
        _applier = applier;
        _imageSynchronizer = imageSynchronizer;
        _logger = logger;
        _deviceId = LocalSyncStateStore.GetOrCreateDeviceId();
    }

    public bool IsEnabled
    {
        get
        {
            var database = GetDatabase();
            return database.S3.Enabled
                && database.SyncId != Guid.Empty
                && !string.IsNullOrWhiteSpace(database.S3.Bucket)
                && !string.IsNullOrWhiteSpace(database.S3.AccessKey)
                && !string.IsNullOrWhiteSpace(database.S3.SecretKey);
        }
    }

    public async Task<SyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return SyncResult.Skipped("Синхронизация S3 отключена.");
        }

        var database = EnsureDatabaseSyncId();
        var state = _stateStore.Load(database.SyncId, _deviceId);
        var databasePath = database.GetResolvedPath();
        var objectStore = CreateObjectStore(database);
        try
        {
            var changed = false;
            changed |= await PullAsync(database, state, databasePath, objectStore, cancellationToken);
            changed |= await PushAsync(database, state, databasePath, objectStore, cancellationToken);
            _stateStore.Save(state);

            if (changed)
            {
                WeakReferenceMessenger.Default.Send(new DatabaseChangedMessage());
            }

            return SyncResult.Completed("Синхронизация завершена.", state.AppliedRevision, state.PublishedRevision);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed for database {SyncId}", database.SyncId);
            return SyncResult.Failed(ex.Message);
        }
        finally
        {
            if (objectStore is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private ISyncObjectStore CreateObjectStore(DatabaseConfiguration database)
    {
        var store = _objectStoreFactory.Create(database.S3);
        return store is IDisposable disposable ? new DisposableObjectStore(store, disposable) : store;
    }

    private async Task<bool> PullAsync(
        DatabaseConfiguration database,
        LocalSyncState state,
        string databasePath,
        ISyncObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        var manifestKey = SyncPaths.ManifestKey(database);
        var (manifestJson, _) = await objectStore.TryDownloadTextAsync(manifestKey, cancellationToken);
        if (manifestJson == null)
        {
            return false;
        }

        var manifest = JsonSerializer.Deserialize<SyncManifest>(manifestJson, JsonOptions)
            ?? throw new InvalidOperationException("Invalid sync manifest.");

        if (manifest.Revision <= state.AppliedRevision)
        {
            return false;
        }

        if (state.AppliedRevision == 0
            && manifest.SnapshotRevision.HasValue
            && manifest.SnapshotRevision > 0
            && !string.IsNullOrWhiteSpace(manifest.SnapshotKey)
            && manifest.SnapshotRevision > state.AppliedRevision)
        {
            await RestoreSnapshotAsync(databasePath, manifest.SnapshotKey, objectStore, cancellationToken);
            state.AppliedRevision = manifest.SnapshotRevision.Value;
        }

        var changed = false;
        for (var revision = state.AppliedRevision + 1; revision <= manifest.Revision; revision++)
        {
            var deltaKey = SyncPaths.DeltaKey(database, revision);
            var (deltaJson, _) = await objectStore.TryDownloadTextAsync(deltaKey, cancellationToken);
            if (deltaJson == null)
            {
                _logger.LogWarning("Missing sync delta {Revision} for database {SyncId}", revision, database.SyncId);
                break;
            }

            var delta = JsonSerializer.Deserialize<SyncDelta>(deltaJson, JsonOptions)
                ?? throw new InvalidOperationException($"Invalid sync delta {revision}.");

            using var db = new LiteDatabase(databasePath);
            _applier.Apply(db, delta);
            if (delta.Images.Count > 0)
            {
                await _imageSynchronizer.ApplyAsync(db, database, objectStore, delta.Images, cancellationToken);
            }

            state.AppliedRevision = revision;
            changed = true;
        }

        return changed;
    }

    private async Task<bool> PushAsync(
        DatabaseConfiguration database,
        LocalSyncState state,
        string databasePath,
        ISyncObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        using var db = new LiteDatabase(databasePath);
        var entries = _outbox.GetAll(db);
        var imageEntries = _imageOutbox.GetAll(db);
        if (entries.Count == 0 && imageEntries.Count == 0)
        {
            return false;
        }

        var manifestKey = SyncPaths.ManifestKey(database);
        var (manifestJson, manifestEtag) = await objectStore.TryDownloadTextAsync(manifestKey, cancellationToken);
        var remoteRevision = 0L;
        if (manifestJson != null)
        {
            var remoteManifest = JsonSerializer.Deserialize<SyncManifest>(manifestJson, JsonOptions);
            remoteRevision = remoteManifest?.Revision ?? 0;
        }

        var newRevision = Math.Max(remoteRevision, state.PublishedRevision) + 1;
        var delta = _outbox.BuildDelta(entries, newRevision, _deviceId);
        _imageOutbox.AddToDelta(delta, imageEntries);
        await _imageSynchronizer.UploadPendingAsync(db, database, objectStore, imageEntries, cancellationToken);

        var deltaJson = JsonSerializer.Serialize(delta, JsonOptions);
        await objectStore.UploadTextAsync(SyncPaths.DeltaKey(database, newRevision), deltaJson, null, cancellationToken);

        string? snapshotKey = null;
        long? snapshotRevision = null;
        if (newRevision % SyncPaths.SnapshotInterval == 0)
        {
            snapshotKey = SyncPaths.SnapshotKey(database, newRevision);
            await objectStore.UploadFileAsync(snapshotKey, databasePath, cancellationToken);
            snapshotRevision = newRevision;
        }
        else if (manifestJson != null)
        {
            var remoteManifest = JsonSerializer.Deserialize<SyncManifest>(manifestJson, JsonOptions);
            snapshotKey = remoteManifest?.SnapshotKey;
            snapshotRevision = remoteManifest?.SnapshotRevision;
        }

        var manifest = new SyncManifest
        {
            Revision = newRevision,
            UpdatedAt = DateTime.UtcNow,
            UpdatedByDevice = _deviceId,
            SnapshotRevision = snapshotRevision,
            SnapshotKey = snapshotKey,
        };

        var newManifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
        await objectStore.UploadTextAsync(manifestKey, newManifestJson, manifestEtag, cancellationToken);

        _outbox.Clear(db);
        _imageOutbox.Clear(db);
        state.PublishedRevision = newRevision;
        state.AppliedRevision = Math.Max(state.AppliedRevision, newRevision);
        return true;
    }

    private static async Task RestoreSnapshotAsync(
        string databasePath,
        string snapshotKey,
        ISyncObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        var tempPath = databasePath + ".sync-restore";
        await objectStore.DownloadFileAsync(snapshotKey, tempPath, cancellationToken);

        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }

        File.Move(tempPath, databasePath, overwrite: true);
    }

    private DatabaseConfiguration GetDatabase() => _configuration.GetSelectedDatabase();

    private DatabaseConfiguration EnsureDatabaseSyncId()
    {
        var database = GetDatabase();
        if (database.SyncId != Guid.Empty)
        {
            return database;
        }

        database.SyncId = Guid.NewGuid();
        var root = _configuration.Get();
        var index = root.SelectedDatabaseIndex;
        if (index >= 0 && index < root.Databases.Length)
        {
            root.Databases[index] = database;
            _configuration.Save(root);
        }

        return database;
    }
}
