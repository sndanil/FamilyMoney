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

public sealed class SyncService : ISyncService
{
    /// <summary>Максимальная версия протокола, которую понимает это приложение.</summary>
    private const int SupportedProtocolVersion = 2;

    private const int MaxPushAttempts = 3;

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

        if (!await _syncGate.WaitAsync(0, cancellationToken))
        {
            return SyncResult.Skipped("Синхронизация уже выполняется.");
        }

        try
        {
            var database = EnsureDatabaseSyncId();
            var state = _stateStore.Load(database.SyncId, _deviceId);
            var databasePath = database.GetResolvedPath();
            var objectStore = _objectStoreFactory.Create(database.S3);
            var operationId = state.PendingOperationId ?? Guid.NewGuid().ToString("N");

            await using var syncLock = await SyncLock.TryAcquireAsync(
                database,
                objectStore,
                _deviceId,
                operationId,
                cancellationToken);

            if (syncLock == null)
            {
                return SyncResult.Skipped("Синхронизация уже выполняется на другом устройстве.");
            }

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
                            "Манифест синхронизации изменён другим устройством. Повторите синхронизацию.");
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
        var manifestKey = SyncPaths.ManifestKey(database);
        var (manifestJson, _) = await objectStore.TryDownloadTextAsync(manifestKey, cancellationToken);
        var manifest = DeserializeManifest(manifestJson);
        if (manifest == null)
        {
            return PullResult.Empty;
        }

        EnsureSupportedProtocol(manifest);

        if (manifest.Revision <= state.AppliedRevision)
        {
            return PullResult.Empty;
        }

        if (state.AppliedRevision == 0
            && manifest.SnapshotRevision.HasValue
            && manifest.SnapshotRevision > 0
            && manifest.SnapshotRevision <= manifest.Revision
            && !string.IsNullOrWhiteSpace(manifest.SnapshotKey))
        {
            // Снапшот заменяет файл базы целиком. Если на устройстве уже есть
            // неотправленные изменения, сначала применяем журнал дельт, иначе
            // они будут потеряны.
            var hasPendingChanges = _repository.GetPendingSyncChanges().HasChanges;
            if (!hasPendingChanges)
            {
                await RestoreSnapshotAsync(
                    database.GetResolvedPath(),
                    manifest.SnapshotKey,
                    objectStore,
                    cancellationToken);
                state.AppliedRevision = manifest.SnapshotRevision.Value;
            }
        }

        var changed = false;

        for (var revision = state.AppliedRevision + 1; revision <= manifest.Revision; revision++)
        {
            var deltaKey = SyncPaths.DeltaKey(database, revision);
            var (deltaJson, _) = await objectStore.TryDownloadTextAsync(deltaKey, cancellationToken);
            if (deltaJson == null)
            {
                _logger.LogWarning(
                    "Missing sync delta {Revision} for database {SyncId}",
                    revision,
                    database.SyncId);
                return new PullResult(
                    changed,
                    true,
                    $"Не найдена дельта ревизии {revision}. Повторите синхронизацию.");
            }

            var delta = JsonSerializer.Deserialize<SyncDelta>(deltaJson, JsonDefaults.Indented)
                ?? throw new InvalidOperationException($"Invalid sync delta {revision}.");

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
                        "Revision {Revision} applied without {Count} images",
                        revision,
                        imagesResult.Failed.Count);
                    return new PullResult(
                        true,
                        true,
                        $"Не удалось загрузить изображения ({imagesResult.Failed.Count}). " +
                        "Синхронизация повторится при следующем запуске.");
                }
            }

            state.AppliedRevision = revision;
            changed = true;
        }

        return new PullResult(changed, false, null);
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
        var manifestKey = SyncPaths.ManifestKey(database);
        var (manifestJson, manifestEtag) = await objectStore.TryDownloadTextAsync(manifestKey, cancellationToken);
        var remoteManifest = DeserializeManifest(manifestJson);
        if (remoteManifest != null)
        {
            EnsureSupportedProtocol(remoteManifest);
        }

        if (remoteManifest?.OperationId == operationId
            && IsPendingOperationComplete(state, pending))
        {
            // Предыдущая попытка уже опубликовала эти изменения, но не успела
            // очистить outbox. Повторяем только локальное завершение.
            _repository.ClearPendingSyncChanges(pending);
            state.PublishedRevision = Math.Max(state.PublishedRevision, remoteManifest.Revision);
            state.AppliedRevision = Math.Max(state.AppliedRevision, remoteManifest.Revision);
            state.PendingOperationId = null;
            state.PendingOperationHash = null;
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

        var newRevision = (remoteManifest?.Revision ?? 0) + 1;
        var delta = BuildDelta(pending, newRevision);
        var deltaKey = SyncPaths.DeltaKey(database, newRevision);
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

        string? newSnapshotKey = null;
        var newSnapshotCreated = false;
        long? snapshotRevision = remoteManifest?.SnapshotRevision;

        if (newRevision % SyncPaths.SnapshotInterval == 0)
        {
            newSnapshotKey = SyncPaths.SnapshotKey(database, newRevision);
            await UploadSnapshotAsync(databasePath, newSnapshotKey, objectStore, cancellationToken);
            newSnapshotCreated = true;
            snapshotRevision = newRevision;
        }

        var manifest = new SyncManifest
        {
            ProtocolVersion = SupportedProtocolVersion,
            Revision = newRevision,
            UpdatedAt = DateTime.UtcNow,
            UpdatedByDevice = _deviceId,
            SnapshotRevision = newSnapshotKey != null
                ? snapshotRevision
                : remoteManifest?.SnapshotRevision,
            SnapshotKey = newSnapshotKey ?? remoteManifest?.SnapshotKey,
            OperationId = operationId,
        };

        try
        {
            await UploadManifestAsync(
                objectStore,
                manifestKey,
                manifest,
                manifestJson != null,
                manifestEtag,
                cancellationToken);
        }
        catch (SyncConflictException)
        {
            // Условная запись не прошла — manifest точно не наш, снапшот
            // никем не упомянут и его можно убрать. Ошибку проверки ниже
            // так не обрабатываем: там manifest мог уже записаться.
            if (newSnapshotCreated && newSnapshotKey != null)
            {
                await TryDeleteAsync(objectStore, newSnapshotKey, cancellationToken);
            }

            throw;
        }

        await VerifyManifestAsync(objectStore, manifestKey, newRevision, operationId, cancellationToken);

        if (newSnapshotCreated
            && newSnapshotKey != null
            && !string.IsNullOrWhiteSpace(remoteManifest?.SnapshotKey)
            && remoteManifest.SnapshotKey != newSnapshotKey)
        {
            await TryDeleteAsync(objectStore, remoteManifest.SnapshotKey, cancellationToken);
        }

        _repository.ClearPendingSyncChanges(pending);
        state.PublishedRevision = newRevision;
        state.AppliedRevision = Math.Max(state.AppliedRevision, newRevision);
        state.PendingOperationId = null;
        state.PendingOperationHash = null;
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

        var manifestKey = SyncPaths.ManifestKey(database);
        var (manifestJson, manifestEtag) = await objectStore.TryDownloadTextAsync(manifestKey, cancellationToken);
        var remoteManifest = DeserializeManifest(manifestJson);

        if (!string.IsNullOrWhiteSpace(remoteManifest?.SnapshotKey))
        {
            return;
        }

        var remoteRevision = remoteManifest?.Revision ?? 0;
        if (remoteRevision > state.AppliedRevision)
        {
            // Локальная база отстаёт от хранилища — снимок с неё был бы неполным.
            return;
        }

        var snapshotRevision = Math.Max(remoteRevision, 1);
        var snapshotKey = SyncPaths.SnapshotKey(database, snapshotRevision);
        await UploadSnapshotAsync(databasePath, snapshotKey, objectStore, cancellationToken);

        var operationId = remoteManifest?.OperationId ?? Guid.NewGuid().ToString("N");
        var manifest = new SyncManifest
        {
            ProtocolVersion = SupportedProtocolVersion,
            Revision = snapshotRevision,
            UpdatedAt = DateTime.UtcNow,
            UpdatedByDevice = _deviceId,
            SnapshotRevision = snapshotRevision,
            SnapshotKey = snapshotKey,
            OperationId = operationId,
        };

        try
        {
            await UploadManifestAsync(
                objectStore,
                manifestKey,
                manifest,
                manifestJson != null,
                manifestEtag,
                cancellationToken);
        }
        catch (SyncConflictException)
        {
            await TryDeleteAsync(objectStore, snapshotKey, cancellationToken);
            throw;
        }

        await VerifyManifestAsync(
            objectStore,
            manifestKey,
            snapshotRevision,
            operationId,
            cancellationToken);

        state.AppliedRevision = Math.Max(state.AppliedRevision, snapshotRevision);
        state.PublishedRevision = Math.Max(state.PublishedRevision, snapshotRevision);

        _logger.LogInformation(
            "Created initial sync snapshot {SnapshotKey} at revision {Revision} for database {SyncId}",
            snapshotKey,
            snapshotRevision,
            database.SyncId);
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

    private async Task UploadManifestAsync(
        ISyncObjectStore objectStore,
        string manifestKey,
        SyncManifest manifest,
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
                "the manifest will be verified after writing");
            await objectStore.UploadTextAsync(manifestKey, json, SyncWriteCondition.None, cancellationToken);
        }
    }

    private static async Task VerifyManifestAsync(
        ISyncObjectStore objectStore,
        string manifestKey,
        long expectedRevision,
        string? expectedOperationId,
        CancellationToken cancellationToken)
    {
        var (json, _) = await objectStore.TryDownloadTextAsync(manifestKey, cancellationToken);
        var actual = DeserializeManifest(json);
        if (actual == null
            || actual.Revision != expectedRevision
            || actual.OperationId != expectedOperationId)
        {
            throw new SyncConflictException(
                "Манифест синхронизации изменён другим устройством.");
        }
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

    private static SyncManifest? DeserializeManifest(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<SyncManifest>(json, JsonDefaults.Indented)
            ?? throw new InvalidOperationException("Invalid sync manifest.");
    }

    private static void EnsureSupportedProtocol(SyncManifest manifest)
    {
        if (manifest.ProtocolVersion > SupportedProtocolVersion)
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

    private readonly record struct PullResult(bool Changed, bool Incomplete, string? Error)
    {
        public static PullResult Empty => new(false, false, null);
    }
}
