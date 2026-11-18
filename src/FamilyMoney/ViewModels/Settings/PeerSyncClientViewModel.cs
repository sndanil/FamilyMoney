using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using FamilyMoney.Services;
using FamilyMoney.State;
using FamilyMoney.Sync;
using FamilyMoney.Sync.Peer;
using Microsoft.Extensions.Logging;

namespace FamilyMoney.ViewModels.Settings;

/// <summary>
/// Android-клиент прямой синхронизации: сканирует QR десктопа и выполняет
/// обычную peer-сессию через IPeerTransport.
/// </summary>
public partial class PeerSyncClientViewModel : ViewModelBase
{
    private readonly IGlobalConfiguration _configuration;
    private readonly IRepository _repository;
    private readonly LocalSyncStateStore _localState;
    private readonly IQrScannerService? _scanner;
    private readonly ILocalNetworkPermissionService? _permission;
    private readonly ILogger<PeerSyncClientViewModel>? _logger;
    private readonly IAccountLocalSettingsStore? _localSettings;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial bool IsSyncing { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = "Откройте «По Wi-Fi» на компьютере и отсканируйте QR.";

    public bool IsAvailable => _scanner != null;

    public PeerSyncClientViewModel(
        IGlobalConfiguration configuration,
        IRepository repository,
        LocalSyncStateStore localState,
        IQrScannerService? scanner,
        ILocalNetworkPermissionService? permission = null,
        ILogger<PeerSyncClientViewModel>? logger = null,
        IAccountLocalSettingsStore? localSettings = null)
    {
        _configuration = configuration;
        _repository = repository;
        _localState = localState;
        _scanner = scanner;
        _permission = permission;
        _logger = logger;
        _localSettings = localSettings;
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    public async Task StartAsync()
    {
        if (_scanner == null || IsSyncing)
        {
            return;
        }

        if (_permission != null && !await _permission.EnsureGrantedAsync())
        {
            Status = "Нет разрешения на доступ к локальной сети. Разрешите его в настройках Android.";
            return;
        }

        var json = await _scanner.ScanAsync();
        if (string.IsNullOrWhiteSpace(json))
        {
            Status = "Сканирование отменено.";
            return;
        }

        var pairing = PeerPairingPayload.TryParse(json);
        if (pairing == null)
        {
            Status = "QR-код не подходит, повреждён или истёк.";
            return;
        }

        var database = _configuration.GetSelectedDatabase();
        if (database.SyncId == Guid.Empty)
        {
            database.SyncId = pairing.SyncId;
            _configuration.Save(_configuration.Get());
        }
        else if (database.SyncId != pairing.SyncId)
        {
            if (_repository.HasLocalData())
            {
                Status = "Это QR от другой базы. Синхронизация отменена, чтобы не смешать данные.";
                return;
            }

            database.SyncId = pairing.SyncId;
            _configuration.Save(_configuration.Get());
        }

        IsSyncing = true;
        Status = "Подключение к компьютеру...";

        try
        {
            // Первая синхронизация переливает всю историю (десятки тысяч записей),
            // поэтому времени нужно заметно больше, чем на инкрементальную.
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
            var stateStore = new PeerSyncStateStore(_localState.HomeFolder);
            var engine = new PeerSyncClientEngine(
                database.SyncId,
                _localState.DeviceId,
                GetDeviceName(),
                _repository,
                stateStore,
                localSettings: _localSettings);

            var endpoint = new PeerServerEndpoint(
                pairing.Host,
                pairing.Port,
                pairing.Fingerprint);

            await using var transport = await PeerSyncClient.ConnectAsync(
                endpoint,
                timeout.Token);

            var progress = new Progress<PeerSyncProgress>(value =>
            {
                Status = value.Phase == "pull"
                    ? $"Получено записей: {value.Entities}, фото: {value.Images}"
                    : $"Отправлено записей: {value.Entities}, фото: {value.Images}";
            });

            var result = await engine.SyncAsync(
                transport,
                pairing.Token,
                progress,
                timeout.Token);

            Status = result.Message;
            WeakReferenceMessenger.Default.Send(new DatabaseChangedMessage());
        }
        catch (OperationCanceledException)
        {
            Status = "Синхронизация отменена или превысила время ожидания.";
        }
        catch (PeerSyncException ex)
        {
            Status = "Ошибка: " + ex.Message;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Peer sync client failed");
            Status = "Не удалось синхронизироваться: " + ex.Message;
        }
        finally
        {
            IsSyncing = false;
        }
    }

    private bool CanStart() => IsAvailable && !IsSyncing;

    private static string GetDeviceName()
    {
        var name = Environment.MachineName;
        return string.IsNullOrWhiteSpace(name)
            || string.Equals(name, "localhost", StringComparison.OrdinalIgnoreCase)
            ? "Телефон"
            : name;
    }
}
