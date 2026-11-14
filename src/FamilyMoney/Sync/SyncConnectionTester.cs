using FamilyMoney.Configuration;
using Microsoft.Extensions.Logging;

namespace FamilyMoney.Sync;

public readonly record struct SyncConnectionTestResult(bool Success, string Message);

/// <summary>
/// Проверяет, что выбранное хранилище доступно на запись и удаление.
/// Используется кнопкой «Проверить подключение» в настройках.
/// </summary>
public sealed class SyncConnectionTester
{
    private readonly ISyncObjectStoreFactory _factory;
    private readonly ILogger<SyncConnectionTester> _logger;

    public SyncConnectionTester(
        ISyncObjectStoreFactory factory,
        ILogger<SyncConnectionTester> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<SyncConnectionTestResult> TestAsync(
        DatabaseConfiguration database,
        CancellationToken cancellationToken = default)
    {
        var sync = database.GetSyncConfiguration();
        if (!sync.HasSettings)
        {
            return new SyncConnectionTestResult(false, "Синхронизация не настроена.");
        }

        if (database.SyncId == Guid.Empty)
        {
            return new SyncConnectionTestResult(false, "У базы нет идентификатора синхронизации.");
        }

        var probeKey = $"{SyncPaths.RootPrefix(database)}/.connection-test";
        try
        {
            var store = _factory.Create(sync);
            await store.UploadTextAsync(
                probeKey,
                DateTime.UtcNow.ToString("O"),
                SyncWriteCondition.None,
                cancellationToken);
            await store.DeleteAsync(probeKey, cancellationToken);
            return new SyncConnectionTestResult(true, "Подключение работает.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sync connection test failed for database {SyncId}", database.SyncId);
            return new SyncConnectionTestResult(false, ex.Message);
        }
    }
}
