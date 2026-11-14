using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using FamilyMoney.Utils;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace FamilyMoney.Sync;

/// <summary>
/// Синхронизация по журналам устройств. Каждое устройство пишет только свой
/// manifest и свои дельты, поэтому writers не конфликтуют за общий файл.
/// Слияние остаётся last-write-wins по LastChange.
/// </summary>
public sealed class SyncService : ISyncService
{
    private const int SupportedProtocolVersion = 3;

    private const int MaxPushAttempts = 3;

    private const int SnapshotKeep = 2;

    private readonly IGlobalConfiguration _configuration;
    private readonly IRepository _repository;
    private readonly ISyncObjectStoreFactory _objectStoreFactory;
    private readonly LocalSyncStateStore _stateStore;
    private readonly SyncImageSynchronizer _imageSynchronizer;
    private readonly ILogger<SyncService> _logger;
    private readonly string _deviceId;
    private readonly SemaphoreSlim _syncGate = new(1, 1);

    public SyncService(
        IGlobalConfiguration configuration,
        IRepository repository,
        ISyncObjectStoreFactory objectStoreFactory,
        LocalSyncStateStore stateStore,
        SyncImageSynchronizer imageSynchronizer,
        ILogger<SyncService> logger)
    {
        _configuration = configuration;
        _repository = repository;
        _objectStoreFactory = objectStoreFactory;
        _stateStore = stateStore;
        _imageSynchronizer = imageSynchronizer;
        _logger = logger;
        _deviceId = stateStore.DeviceId;
    }

    public bool IsEnabled
    {
        get
        {
            var database = GetDatabase();
            var sync = database.GetSyncConfiguration();
            return database.SyncId != Guid.Empty && sync.IsConfigured;
        }
    }

    public async Task<SyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return SyncResult.Skipped("Синхронизация отключена или не настроена.");
        }

        if (!await _syncGate.WaitAsync(0, cancellationToken))
        {
            return SyncResult.Skipped("Синхронизация уже выполняется.");
        }

        try
        {
            var database = EnsureDatabaseSyncId();
            var state = _stateStore.Load(database.SyncId, _deviceId);
            state.AppliedByDevice ??= new Dictionary<string, long>(StringComparer.Ordinal);

            if (!state.LegacyMigrated
                && state.LegacyAppliedRevision == 0
                && state.AppliedRevision > 0)
            {
                state.LegacyAppliedRevision = state.AppliedRevision;
            }

            var databasePath = database.GetResolvedPath();
            var objectStore = _objectStoreFactory.Create(database.GetSyncConfiguration());
            var operationId = state.PendingOperationId ?? Guid.NewGuid().ToString("N");
            var changed = false;

            for (var attempt = 1; attempt <= MaxPushAttempts; attempt++)
            {
                var pull = await PullAsync(database, state, objectStore, cancellationToken);
                changed |= pull.Changed;

                if (pull.Incomplete)
                {
                    _stateStore.Save(state);

                    if (changed)
                    {
                        WeakReferenceMessenger.Default.Send(new DatabaseChangedMessage());
                    }

                    return SyncResult.Failed(pull.Error ?? "Синхронизация не завершена.");
                }

                try
                {
                    changed |= await PushAsync(
                        database,
                        state,
                        databasePath,
                        objectStore,
                        operationId,
                        cancellationToken);

                    await EnsureSnapshotAsync(
                        database,
                        state,
                        databasePath,
                        objectStore,
                        cancellationToken);

                    _stateStore.Save(state);

                    if (changed)
                    {
                        WeakReferenceMessenger.Default.Send(new DatabaseChangedMessage());
                    }

                    return SyncResult.Completed(
                        "Синхронизация завершена.",
                        state.AppliedRevision,
                        state.PublishedRevision);
                }
                catch (SyncConflictException ex)
                {
                    _stateStore.Save(state);

                    if (attempt == MaxPushAttempts)
                    {
                        _logger.LogWarning(ex, "Sync conflict after {Attempts} attempts", attempt);
                        return SyncResult.Failed(
                            "Manifest этого устройства изменён параллельным процессом. Повторите синхронизацию.");
                    }

                    _logger.LogWarning(ex, "Sync conflict, retrying ({Attempt}/{Max})", attempt, MaxPushAttempts);
                }
            }

            return SyncResult.Failed("Не удалось завершить синхронизацию.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed");
            return SyncResult.Failed(ex.Message);
        }
        finally
        {
            _syncGate.Release();
        }
    }

    private async Task<PullResult> PullAsync(
        DatabaseConfiguration database,
        LocalSyncState state,
        ISyncObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        var changed = false;

        // Новое устройство без истории: сначала пробуем общий снапшот.
        var isNewDevice = state.AppliedByDevice.Count == 0
            && state.LegacyAppliedRevision == 0
            && state.AppliedRevision == 0
            && !_repository.GetPendingSyncChanges().HasChanges
            && !_repository.HasLocalData();

        if (isNewDevice
            && await TryRestoreNewSnapshotAsync(database, state, objectStore, cancellationToken))
        {
            changed = true;
        }

        if (!state.LegacyMigrated)
        {
            var legacy = await PullLegacyAsync(database, state, objectStore, cancellationToken);
            changed |= legacy.Changed;

            if (legacy.Incomplete)
            {
                return new PullResult(changed, true, legacy.Error);
            }

            state.LegacyMigrated = true;
        }

        var journal = await PullJournalsAsync(database, state, objectStore, cancellationToken);
        changed |= journal.Changed;

        if (journal.Incomplete)
        {
            return new PullResult(changed, true, journal.Error);
        }

        UpdateAggregate(state);
        return new PullResult(changed, false, null);
    }

    private async Task<PullResult> PullLegacyAsync(
        DatabaseConfiguration database,
        LocalSyncState state,
        ISyncObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        var (manifestJson, _) = await objectStore.TryDownloadTextAsync(
            SyncPaths.ManifestKey(database),
            cancellationToken);

        var manifest = DeserializeLegacyManifest(manifestJson);
        if (manifest == null)
        {
            return PullResult.Empty;
        }

        EnsureSupportedProtocol(manifest.ProtocolVersion);

        var changed = false;

        if (state.LegacyAppliedRevision == 0
            && manifest.SnapshotRevision.HasValue
            && manifest.SnapshotRevision.Value > 0
            && manifest.SnapshotRevision.Value <= manifest.Revision
            && !string.IsNullOrWhiteSpace(manifest.SnapshotKey)
            && !_repository.GetPendingSyncChanges().HasChanges
            && !_repository.HasLocalData())
        {
            await RestoreSnapshotAsync(
                database.GetResolvedPath(),
                manifest.SnapshotKey,
                objectStore,
                cancellationToken);
            state.LegacyAppliedRevision = manifest.SnapshotRevision.Value;
            changed = true;
        }

        for (var revision = state.LegacyAppliedRevision + 1; revision <= manifest.Revision; revision++)
        {
            var (deltaJson, _) = await objectStore.TryDownloadTextAsync(
                SyncPaths.DeltaKey(database, revision),
                cancellationToken);

            if (deltaJson == null)
            {
                _logger.LogWarning(
                    "Missing legacy sync delta {Revision} for database {SyncId}",
                    revision,
                    database.SyncId);
                return new PullResult(
                    changed,
                    true,
                    $"Не найдена старая дельта ревизии {revision}. Повторите синхронизацию.");
            }

            var delta = DeserializeDelta(deltaJson, revision);
            var applied = await ApplyDeltaAsync(database, state, objectStore, delta, cancellationToken);
            changed |= applied.Changed;

            if (applied.Incomplete)
            {
                return applied;
            }

            state.LegacyAppliedRevision = revision;
        }

        UpdateAggregate(state);
        _logger.LogInformation(
            "Legacy journal migrated at revision {Revision} for database {SyncId}",
            state.LegacyAppliedRevision,
            database.SyncId);

        return new PullResult(changed, false, null);
    }

    private async Task<PullResult> PullJournalsAsync(
        DatabaseConfiguration database,
        LocalSyncState state,
        ISyncObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        var manifests = await ListDeviceManifestsAsync(database, objectStore, cancellationToken);
        var groups = new Dictionary<string, List<PendingDelta>>(StringComparer.Ordinal);

        foreach (var manifest in manifests)
        {
            var applied = state.AppliedByDevice.GetValueOrDefault(manifest.DeviceId);
            if (applied > manifest.LastSequence)
            {
                _logger.LogWarning(
                    "Device {DeviceId} journal moved backwards; resetting local position",
                    manifest.DeviceId);
                applied = manifest.LastSequence;
                state.AppliedByDevice[manifest.DeviceId] = applied;
            }

            for (var sequence = applied + 1; sequence <= manifest.LastSequence; sequence++)
            {
                var key = SyncPaths.DeviceDeltaKey(database, manifest.DeviceId, sequence);
                var (json, _) = await objectStore.TryDownloadTextAsync(key, cancellationToken);
                if (json == null)
                {
                    _logger.LogWarning(
                        "Missing device delta {DeviceId}/{Sequence} for database {SyncId}",
                        manifest.DeviceId,
                        sequence,
                        database.SyncId);
                    return new PullResult(
                        false,
                        true,
                        $"Не найдена дельта устройства {manifest.DeviceId} №{sequence}. " +
                        "Повторите синхронизацию.");
                }

                var delta = DeserializeDelta(json, sequence);
                if (!string.Equals(delta.DeviceId, manifest.DeviceId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Дельта {sequence} принадлежит устройству {delta.DeviceId}, " +
                        $"а не {manifest.DeviceId}.");
                }

                if (!groups.TryGetValue(manifest.DeviceId, out var list))
                {
                    list = [];
                    groups[manifest.DeviceId] = list;
                }

                list.Add(new PendingDelta(manifest.DeviceId, sequence, delta));
            }
        }

        var changed = false;
        var orderedGroups = groups
            .Select(pair => new
            {
                DeviceId = pair.Key,
                Deltas = pair.Value.OrderBy(item => item.Sequence).ToList(),
                FirstCreatedAt = pair.Value.Min(item => item.Delta.CreatedAt),
            })
            .OrderBy(item => item.FirstCreatedAt)
            .ThenBy(item => item.DeviceId, StringComparer.Ordinal);

        foreach (var group in orderedGroups)
        {
            foreach (var pending in group.Deltas)
            {
                var applied = await ApplyDeltaAsync(
                    database,
                    state,
                    objectStore,
                    pending.Delta,
                    cancellationToken);
                changed |= applied.Changed;

                if (applied.Incomplete)
                {
                    return applied;
                }

                state.AppliedByDevice[pending.DeviceId] = pending.Sequence;
            }
        }

        UpdateAggregate(state);
        return new PullResult(changed, false, null);
    }

    private async Task<PullResult> ApplyDeltaAsync(
        DatabaseConfiguration database,
        LocalSyncState state,
        ISyncObjectStore objectStore,
        SyncDelta delta,
        CancellationToken cancellationToken)
    {
        _repository.ApplySyncDelta(delta);

        if (delta.Images.Count > 0)
        {
            var imagesResult = await _repository.ApplySyncedImagesAsync(
                delta.Images,
                async (image, ct) => await objectStore.DownloadStreamAsync(
                    SyncPaths.ImageKey(database, image.EntityId),
                    ct),
                cancellationToken);

            if (imagesResult.HasFailures)
            {
                _logger.LogWarning(
                    "Delta {DeviceId}/{Revision} applied without {Count} images",
                    delta.DeviceId,
                    delta.Revision,
                    imagesResult.Failed.Count);
                return new PullResult(
                    true,
                    true,
                    $"Не удалось загрузить изображения ({imagesResult.Failed.Count}). " +
                    "Синхронизация повторится при следующем запуске.");
            }
        }

        return new PullResult(true, false, null);
    }

    private async Task<bool> PushAsync(
        DatabaseConfiguration database,
        LocalSyncState state,
        string databasePath,
        ISyncObjectStore objectStore,
        string operationId,
        CancellationToken cancellationToken)
    {
        var pending = _repository.GetPendingSyncChanges();
        var manifestKey = SyncPaths.DeviceManifestKey(database, _deviceId);
        var (manifestJson, manifestEtag) = await objectStore.TryDownloadTextAsync(
            manifestKey,
            cancellationToken);
        var ownManifest = DeserializeDeviceManifest(manifestJson);

        if (ownManifest != null)
        {
            EnsureSupportedProtocol(ownManifest.ProtocolVersion);
            if (!string.Equals(ownManifest.DeviceId, _deviceId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Manifest устройства принадлежит другому устройству.");
            }
        }

        if (ownManifest?.LastOperationId == operationId
            && IsPendingOperationComplete(state, pending))
        {
            _repository.ClearPendingSyncChanges(pending);
            state.AppliedByDevice[_deviceId] = ownManifest.LastSequence;
            state.PublishedRevision = Math.Max(state.PublishedRevision, ownManifest.LastSequence);
            state.PendingOperationId = null;
            state.PendingOperationHash = null;
            UpdateAggregate(state);
            _stateStore.Save(state);
            _logger.LogInformation("Push {OperationId} was already published", operationId);
            return false;
        }

        if (!pending.HasChanges)
        {
            if (state.PendingOperationId != null || state.PendingOperationHash != null)
            {
                state.PendingOperationId = null;
                state.PendingOperationHash = null;
                _stateStore.Save(state);
            }

            return false;
        }

        var sequence = (ownManifest?.LastSequence ?? 0) + 1;
        var delta = BuildDelta(pending, sequence);
        var deltaKey = SyncPaths.DeviceDeltaKey(database, _deviceId, sequence);
        var deltaJson = JsonSerializer.Serialize(delta, JsonDefaults.Indented);

        state.PendingOperationId = operationId;
        state.PendingOperationHash = ComputeHash(delta);
        _stateStore.Save(state);

        await _imageSynchronizer.UploadPendingAsync(
            database,
            objectStore,
            pending.Images,
            cancellationToken);

        await objectStore.UploadTextAsync(deltaKey, deltaJson, SyncWriteCondition.None, cancellationToken);

        var manifest = new DeviceSyncManifest
        {
            ProtocolVersion = SupportedProtocolVersion,
            DeviceId = _deviceId,
            LastSequence = sequence,
            UpdatedAt = DateTime.UtcNow,
            LastOperationId = operationId,
        };

        await UploadDeviceManifestAsync(
            objectStore,
            manifestKey,
            manifest,
            manifestJson != null,
            manifestEtag,
            cancellationToken);

        await VerifyDeviceManifestAsync(
            objectStore,
            manifestKey,
            sequence,
            operationId,
            cancellationToken);

        _repository.ClearPendingSyncChanges(pending);
        state.AppliedByDevice[_deviceId] = sequence;
        state.PublishedRevision = sequence;
        state.PendingOperationId = null;
        state.PendingOperationHash = null;
        UpdateAggregate(state);
        _stateStore.Save(state);

        return true;
    }

    private async Task EnsureSnapshotAsync(
        DatabaseConfiguration database,
        LocalSyncState state,
        string databasePath,
        ISyncObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(databasePath))
        {
            return;
        }

        var ownSequence = state.AppliedByDevice.GetValueOrDefault(_deviceId);
        var snapshots = await ListSnapshotEntriesAsync(database, objectStore, cancellationToken);
        var due = snapshots.Count == 0
            || (ownSequence > 0
                && ownSequence % SyncPaths.SnapshotInterval == 0
                && state.LastSnapshotOwnSequence < ownSequence);

        if (!due)
        {
            return;
        }

        await CreateSnapshotAsync(database, state, databasePath, objectStore, cancellationToken);

        try
        {
            await CleanupSnapshotsAsync(database, objectStore, cancellationToken);
        }
        catch (Exception ex)
        {
            // Снапшот уже создан; уборка старых не должна отменять синхронизацию.
            _logger.LogWarning(ex, "Snapshot cleanup failed");
        }
    }

    private async Task CreateSnapshotAsync(
        DatabaseConfiguration database,
        LocalSyncState state,
        string databasePath,
        ISyncObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        var snapshotId = $"{DateTime.UtcNow.Ticks:D19}-{_deviceId}";
        var snapshotKey = SyncPaths.SnapshotKey(database, snapshotId);
        var metadataKey = SyncPaths.SnapshotMetadataKey(database, snapshotId);

        await UploadSnapshotAsync(databasePath, snapshotKey, objectStore, cancellationToken);

        var metadata = new SyncSnapshotMetadata
        {
            ProtocolVersion = SupportedProtocolVersion,
            CreatedAt = DateTime.UtcNow,
            DeviceId = _deviceId,
            AppliedByDevice = new Dictionary<string, long>(
                state.AppliedByDevice,
                StringComparer.Ordinal),
        };

        try
        {
            await objectStore.UploadTextAsync(
                metadataKey,
                JsonSerializer.Serialize(metadata, JsonDefaults.Indented),
                SyncWriteCondition.None,
                cancellationToken);
        }
        catch
        {
            await TryDeleteAsync(objectStore, snapshotKey, cancellationToken);
            throw;
        }

        state.LastSnapshotOwnSequence = state.AppliedByDevice.GetValueOrDefault(_deviceId);
        _logger.LogInformation(
            "Created sync snapshot {SnapshotKey} for database {SyncId}",
            snapshotKey,
            database.SyncId);
    }

    private async Task<bool> TryRestoreNewSnapshotAsync(
        DatabaseConfiguration database,
        LocalSyncState state,
        ISyncObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        var snapshots = await ListSnapshotEntriesAsync(database, objectStore, cancellationToken);
        foreach (var snapshot in snapshots.OrderByDescending(item => item.Metadata.CreatedAt))
        {
            var databaseKey = SyncPaths.SnapshotKey(database, snapshot.Id);
            if (!await objectStore.ExistsAsync(databaseKey, cancellationToken))
            {
                continue;
            }

            try
            {
                await RestoreSnapshotAsync(
                    database.GetResolvedPath(),
                    databaseKey,
                    objectStore,
                    cancellationToken);
            }
            catch (SyncEncryptionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Snapshot {SnapshotId} could not be restored", snapshot.Id);
                continue;
            }

            state.AppliedByDevice = new Dictionary<string, long>(
                snapshot.Metadata.AppliedByDevice,
                StringComparer.Ordinal);
            state.LegacyMigrated = true;
            state.LegacyAppliedRevision = Math.Max(
                state.LegacyAppliedRevision,
                snapshot.Metadata.AppliedByDevice.GetValueOrDefault("legacy"));
            UpdateAggregate(state);

            _logger.LogInformation(
                "Restored sync snapshot {SnapshotId} for database {SyncId}",
                snapshot.Id,
                database.SyncId);
            return true;
        }

        return false;
    }

    private async Task<List<(string Id, SyncSnapshotMetadata Metadata)>> ListSnapshotEntriesAsync(
        DatabaseConfiguration database,
        ISyncObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        var result = new List<(string Id, SyncSnapshotMetadata Metadata)>();
        var objects = await objectStore.ListAsync(
            SyncPaths.SnapshotsPrefix(database) + "/",
            cancellationToken);

        foreach (var item in objects.Where(entry => entry.Key.EndsWith(".json", StringComparison.Ordinal)))
        {
            var (json, _) = await objectStore.TryDownloadTextAsync(item.Key, cancellationToken);
            var metadata = DeserializeSnapshotMetadata(json);
            if (metadata == null)
            {
                continue;
            }

            EnsureSupportedProtocol(metadata.ProtocolVersion);
            var fileName = Path.GetFileNameWithoutExtension(item.Key);
            if (!fileName.StartsWith("base-", StringComparison.Ordinal))
            {
                continue;
            }

            result.Add((fileName["base-".Length..], metadata));
        }

        return result;
    }

    private async Task CleanupSnapshotsAsync(
        DatabaseConfiguration database,
        ISyncObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        var snapshots = await ListSnapshotEntriesAsync(database, objectStore, cancellationToken);
        foreach (var snapshot in snapshots
                     .OrderByDescending(item => item.Metadata.CreatedAt)
                     .Skip(SnapshotKeep))
        {
            await TryDeleteAsync(
                objectStore,
                SyncPaths.SnapshotKey(database, snapshot.Id),
                cancellationToken);
            await TryDeleteAsync(
                objectStore,
                SyncPaths.SnapshotMetadataKey(database, snapshot.Id),
                cancellationToken);
        }
    }

    private async Task<List<DeviceSyncManifest>> ListDeviceManifestsAsync(
        DatabaseConfiguration database,
        ISyncObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        var result = new List<DeviceSyncManifest>();
        var prefix = SyncPaths.DevicesPrefix(database).TrimEnd('/') + "/";
        var objects = await objectStore.ListAsync(
            SyncPaths.DevicesPrefix(database) + "/",
            cancellationToken);

        foreach (var item in objects.Where(entry =>
                     entry.Key.EndsWith("/manifest.json", StringComparison.Ordinal)))
        {
            if (!item.Key.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var rest = item.Key[prefix.Length..];
            var slash = rest.IndexOf('/');
            if (slash <= 0)
            {
                continue;
            }

            var deviceId = rest[..slash];
            var (json, _) = await objectStore.TryDownloadTextAsync(item.Key, cancellationToken);
            var manifest = DeserializeDeviceManifest(json);
            if (manifest == null)
            {
                continue;
            }

            EnsureSupportedProtocol(manifest.ProtocolVersion);
            if (!string.Equals(manifest.DeviceId, deviceId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Manifest устройства {deviceId} содержит чужой идентификатор {manifest.DeviceId}.");
            }

            result.Add(manifest);
        }

        return result;
    }

    private async Task UploadDeviceManifestAsync(
        ISyncObjectStore objectStore,
        string manifestKey,
        DeviceSyncManifest manifest,
        bool manifestExists,
        string? manifestEtag,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(manifest, JsonDefaults.Indented);
        var condition = manifestExists
            ? (string.IsNullOrWhiteSpace(manifestEtag)
                ? SyncWriteCondition.None
                : SyncWriteCondition.IfMatch(manifestEtag))
            : SyncWriteCondition.CreateOnly;

        try
        {
            await objectStore.UploadTextAsync(manifestKey, json, condition, cancellationToken);
        }
        catch (SyncWriteNotSupportedException)
        {
            _logger.LogWarning(
                "Conditional write is not supported by the sync store; " +
                "the device manifest will be verified after writing");
            await objectStore.UploadTextAsync(manifestKey, json, SyncWriteCondition.None, cancellationToken);
        }
    }

    private static async Task VerifyDeviceManifestAsync(
        ISyncObjectStore objectStore,
        string manifestKey,
        long expectedSequence,
        string expectedOperationId,
        CancellationToken cancellationToken)
    {
        var (json, _) = await objectStore.TryDownloadTextAsync(manifestKey, cancellationToken);
        var actual = DeserializeDeviceManifest(json);
        if (actual == null
            || actual.LastSequence != expectedSequence
            || !string.Equals(actual.LastOperationId, expectedOperationId, StringComparison.Ordinal))
        {
            throw new SyncConflictException(
                "Manifest устройства изменён параллельным процессом.");
        }
    }

    private async Task UploadSnapshotAsync(
        string databasePath,
        string snapshotKey,
        ISyncObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        _repository.Checkpoint();
        await objectStore.UploadFileAsync(snapshotKey, databasePath, cancellationToken);
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

    private static async Task TryDeleteAsync(
        ISyncObjectStore objectStore,
        string key,
        CancellationToken cancellationToken)
    {
        try
        {
            await objectStore.DeleteAsync(key, cancellationToken);
        }
        catch (Exception)
        {
            // Уборка не должна скрывать исходную ошибку синхронизации.
        }
    }

    private bool IsPendingOperationComplete(LocalSyncState state, SyncPendingChanges pending)
    {
        if (string.IsNullOrWhiteSpace(state.PendingOperationId))
        {
            return !pending.HasChanges;
        }

        if (!pending.HasChanges)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(state.PendingOperationHash))
        {
            return false;
        }

        var delta = BuildDelta(pending, revision: 0);
        return string.Equals(
            ComputeHash(delta),
            state.PendingOperationHash,
            StringComparison.Ordinal);
    }

    private SyncDelta BuildDelta(SyncPendingChanges pending, long revision)
    {
        var delta = SyncDeltaBuilder.Build(pending.Entities, revision, _deviceId);
        SyncDeltaBuilder.AddImages(delta, pending.Images);
        return delta;
    }

    private static string ComputeHash(SyncDelta delta)
    {
        var entities = delta.Accounts
            .Concat(delta.Categories)
            .Concat(delta.SubCategories)
            .Concat(delta.Transactions)
            .OrderBy(x => x.EntityType, StringComparer.Ordinal)
            .ThenBy(x => x.Id)
            .ToList();

        var images = delta.Images
            .OrderBy(x => x.EntityId)
            .ToList();

        var json = JsonSerializer.Serialize(new { entities, images }, JsonDefaults.Compact);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    private static void UpdateAggregate(LocalSyncState state)
    {
        var own = state.AppliedByDevice.GetValueOrDefault(state.DeviceId);
        state.AppliedRevision = Math.Max(
            state.LegacyAppliedRevision,
            state.AppliedByDevice.Count == 0 ? 0 : state.AppliedByDevice.Values.Max());
        state.PublishedRevision = Math.Max(state.PublishedRevision, own);
    }

    private static SyncDelta DeserializeDelta(string? json, long expectedRevision)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException($"Пустая дельта ревизии {expectedRevision}.");
        }

        var delta = JsonSerializer.Deserialize<SyncDelta>(json, JsonDefaults.Indented)
            ?? throw new InvalidOperationException($"Некорректная дельта ревизии {expectedRevision}.");

        return delta;
    }

    private static DeviceSyncManifest? DeserializeDeviceManifest(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<DeviceSyncManifest>(json, JsonDefaults.Indented)
            ?? throw new InvalidOperationException("Invalid device sync manifest.");
    }

    private static SyncManifest? DeserializeLegacyManifest(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<SyncManifest>(json, JsonDefaults.Indented)
            ?? throw new InvalidOperationException("Invalid sync manifest.");
    }

    private static SyncSnapshotMetadata? DeserializeSnapshotMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var metadata = JsonSerializer.Deserialize<SyncSnapshotMetadata>(json, JsonDefaults.Indented)
            ?? throw new InvalidOperationException("Invalid sync snapshot metadata.");
        metadata.AppliedByDevice ??= new Dictionary<string, long>(StringComparer.Ordinal);
        return metadata;
    }

    private static void EnsureSupportedProtocol(int protocolVersion)
    {
        if (protocolVersion > SupportedProtocolVersion)
        {
            throw new InvalidOperationException(
                "База синхронизирована более новой версией приложения. Обновите приложение.");
        }
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

    private sealed record PendingDelta(string DeviceId, long Sequence, SyncDelta Delta);

    private readonly record struct PullResult(bool Changed, bool Incomplete, string? Error)
    {
        public static PullResult Empty => new(false, false, null);
    }
}
