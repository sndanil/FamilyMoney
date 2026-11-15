using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using Microsoft.Extensions.Logging;

namespace FamilyMoney.Sync.Peer;

/// <summary>
/// Публикует в облако изменения, которые десктоп принял по Wi-Fi.
/// Вызывается после завершения P2P-сессии, когда gate уже свободен.
/// </summary>
public sealed class PeerCloudPublisher
{
    private readonly IGlobalConfiguration _configuration;
    private readonly IRepository _repository;
    private readonly ISyncService _syncService;
    private readonly ILogger<PeerCloudPublisher>? _logger;

    public PeerCloudPublisher(
        IGlobalConfiguration configuration,
        IRepository repository,
        ISyncService syncService,
        ILogger<PeerCloudPublisher>? logger = null)
    {
        _configuration = configuration;
        _repository = repository;
        _syncService = syncService;
        _logger = logger;
    }

    public bool IsEnabled
    {
        get
        {
            var database = _configuration.GetSelectedDatabase();
            var sync = database.GetSyncConfiguration();
            return sync.PublishPeerChangesToCloud && _syncService.IsEnabled;
        }
    }

    public async Task<PeerCloudPublishResult> PublishAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return PeerCloudPublishResult.Skipped(
                "Публикация P2P-изменений в облако не настроена.");
        }

        if (!_repository.GetPendingSyncChanges().HasChanges)
        {
            return PeerCloudPublishResult.Skipped("Нет изменений для публикации в облако.");
        }

        var result = await _syncService.SyncAsync(cancellationToken);
        _logger?.LogInformation(
            "Peer cloud publish finished: success={Success}, message={Message}",
            result.Success,
            result.Message);

        return new PeerCloudPublishResult(true, result.Success, result.Message, result);
    }
}
