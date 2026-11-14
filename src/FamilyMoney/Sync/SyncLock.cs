using FamilyMoney.Configuration;
using FamilyMoney.Utils;
using System.Text.Json;

namespace FamilyMoney.Sync;

/// <summary>
/// Распределённая блокировка синхронизации в самом хранилище.
/// Не даёт двум устройствам одновременно менять manifest и журнал ревизий.
/// Основана на условной записи; если хранилище её не поддерживает, блокировка
/// вырождается в no-op, а от гонок остаётся только проверка manifest после записи.
/// </summary>
public sealed class SyncLock : IAsyncDisposable
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    private readonly ISyncObjectStore _store;
    private readonly string? _key;
    private readonly string _deviceId;
    private readonly string _operationId;
    private bool _released;

    private SyncLock(
        ISyncObjectStore store,
        string? key,
        string deviceId,
        string operationId)
    {
        _store = store;
        _key = key;
        _deviceId = deviceId;
        _operationId = operationId;
    }

    public static async Task<SyncLock?> TryAcquireAsync(
        DatabaseConfiguration database,
        ISyncObjectStore store,
        string deviceId,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        if (!store.SupportsConditionalWrite)
        {
            // Хранилище не умеет условную запись — распределённую блокировку
            // сделать нечем. Локальный семафор в SyncService всё равно работает.
            return new SyncLock(store, null, deviceId, operationId);
        }

        var key = SyncPaths.LockKey(database);
        var content = JsonSerializer.Serialize(
            new SyncLockInfo
            {
                DeviceId = deviceId,
                OperationId = operationId,
                ExpiresAt = DateTime.UtcNow.Add(Ttl),
            },
            JsonDefaults.Compact);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await store.UploadTextAsync(key, content, SyncWriteCondition.CreateOnly, cancellationToken);
                if (await IsOwnedByUsAsync(store, key, deviceId, operationId, cancellationToken))
                {
                    return new SyncLock(store, key, deviceId, operationId);
                }

                return null;
            }
            catch (SyncConflictException)
            {
                var (existingJson, _) = await store.TryDownloadTextAsync(key, cancellationToken);
                var existing = TryParse(existingJson);
                if (existing != null && existing.DeviceId == deviceId)
                {
                    // Блокировка этого же устройства: либо повторный вход после
                    // сбоя, либо предыдущий запуск не успел её снять. Забираем
                    // её себе и продлеваем, иначе устройство ждало бы TTL.
                    await store.UploadTextAsync(key, content, SyncWriteCondition.None, cancellationToken);
                    return new SyncLock(store, key, deviceId, operationId);
                }

                if (existing == null || existing.ExpiresAt <= DateTime.UtcNow)
                {
                    await TryDeleteAsync(store, key, cancellationToken);
                    continue;
                }

                return null;
            }
            catch (SyncWriteNotSupportedException)
            {
                return new SyncLock(store, null, deviceId, operationId);
            }
        }

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_released || _key == null)
        {
            return;
        }

        _released = true;
        try
        {
            var (json, _) = await _store.TryDownloadTextAsync(_key);
            var current = TryParse(json);
            if (current != null
                && current.DeviceId == _deviceId
                && current.OperationId == _operationId)
            {
                await _store.DeleteAsync(_key);
            }
        }
        catch (Exception)
        {
            // Освобождение блокировки не должно ломать завершение синхронизации.
            // Просроченную блокировку снимет следующее устройство.
        }
    }

    private static async Task<bool> IsOwnedByUsAsync(
        ISyncObjectStore store,
        string key,
        string deviceId,
        string operationId,
        CancellationToken cancellationToken)
    {
        var (json, _) = await store.TryDownloadTextAsync(key, cancellationToken);
        var current = TryParse(json);
        return current != null
            && current.DeviceId == deviceId
            && current.OperationId == operationId;
    }

    private static async Task TryDeleteAsync(
        ISyncObjectStore store,
        string key,
        CancellationToken cancellationToken)
    {
        try
        {
            await store.DeleteAsync(key, cancellationToken);
        }
        catch (Exception)
        {
            // Следующая попытка снова увидит просроченную блокировку.
        }
    }

    private static SyncLockInfo? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SyncLockInfo>(json, JsonDefaults.Compact);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class SyncLockInfo
    {
        public string DeviceId { get; set; } = string.Empty;

        public string OperationId { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }
    }
}
