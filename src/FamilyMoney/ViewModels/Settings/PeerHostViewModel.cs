using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using FamilyMoney.Sync;
using FamilyMoney.Sync.Peer;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace FamilyMoney.ViewModels.Settings;

/// <summary>
/// Состояние десктопного Wi-Fi-хоста: запуск сервера, QR, ожидание телефона,
/// публикация принятых изменений в S3.
/// </summary>
public partial class PeerHostViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly IGlobalConfiguration _configuration;
    private readonly IRepository _repository;
    private readonly LocalSyncStateStore _localState;
    private readonly ISyncService _syncService;
    private readonly PeerCloudPublisher _publisher;
    private readonly SyncOperationGate _operationGate;
    private readonly ILogger<PeerHostViewModel>? _logger;

    private CancellationTokenSource? _sessionCts;
    private Task? _sessionTask;

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = "Готово к запуску.";

    [ObservableProperty]
    public partial string PairingJson { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EndpointText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ExpiresText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial PeerNetworkAddress? SelectedAddress { get; set; }

    public ObservableCollection<PeerNetworkAddress> Addresses { get; } = [];

    public bool HasPairing => !string.IsNullOrEmpty(PairingJson);

    public PeerHostViewModel(
        IGlobalConfiguration configuration,
        IRepository repository,
        LocalSyncStateStore localState,
        ISyncService syncService,
        PeerCloudPublisher publisher,
        SyncOperationGate operationGate,
        ILogger<PeerHostViewModel>? logger = null)
    {
        _configuration = configuration;
        _repository = repository;
        _localState = localState;
        _syncService = syncService;
        _publisher = publisher;
        _operationGate = operationGate;
        _logger = logger;
        RefreshAddresses();
    }

    partial void OnPairingJsonChanged(string value) =>
        OnPropertyChanged(nameof(HasPairing));

    [RelayCommand]
    public void RefreshAddresses()
    {
        var selected = SelectedAddress?.Address;
        Addresses.Clear();
        foreach (var address in PeerNetworkAddresses.GetAvailable())
        {
            Addresses.Add(address);
        }

        SelectedAddress = Addresses.FirstOrDefault(item => item.Address == selected)
            ?? Addresses.FirstOrDefault();
    }

    [RelayCommand]
    public async Task StartAsync()
    {
        if (IsRunning)
        {
            return;
        }

        var address = SelectedAddress;
        if (address == null)
        {
            Status = "Не найден сетевой IPv4-адрес. Проверьте Wi-Fi/Ethernet.";
            return;
        }

        var database = _configuration.GetSelectedDatabase();
        if (database.SyncId == Guid.Empty)
        {
            database.SyncId = Guid.NewGuid();
            _configuration.Save(_configuration.Get());
        }

        var sync = database.GetSyncConfiguration();
        var publishReceivedChanges = sync.PublishPeerChangesToCloud && _syncService.IsEnabled;
        var token = PeerPairingPayload.CreateToken();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(10);

        var engine = new PeerSyncServerEngine(
            database.SyncId,
            _localState.DeviceId,
            GetDeviceName(),
            token,
            expiresAt,
            _repository,
            new PeerSyncStateStore(_localState.HomeFolder),
            logger: null,
            publishReceivedChanges: publishReceivedChanges);

        PeerSyncServer server;
        try
        {
            server = new PeerSyncServer(engine, address.IpAddress);
        }
        catch (Exception ex)
        {
            Status = "Не удалось создать Wi-Fi-сервер: " + ex.Message;
            return;
        }

        PeerServerEndpoint endpoint;
        try
        {
            endpoint = server.Start();
        }
        catch (Exception ex)
        {
            await server.DisposeAsync();
            Status = "Не удалось начать прослушивание: " + ex.Message;
            return;
        }

        var lease = await _operationGate.TryEnterAsync();
        if (lease == null)
        {
            await server.DisposeAsync();
            Status = "Сейчас уже выполняется другая синхронизация.";
            return;
        }

        _sessionCts = new CancellationTokenSource();

        var payload = new PeerPairingPayload
        {
            SyncId = database.SyncId,
            Host = endpoint.Host,
            Port = endpoint.Port,
            Fingerprint = endpoint.Fingerprint,
            Token = token,
            DeviceId = _localState.DeviceId,
            DeviceName = GetDeviceName(),
            ExpiresAt = expiresAt,
        };

        PairingJson = payload.ToJson();
        EndpointText = $"{address.DisplayName} · {endpoint.Host}:{endpoint.Port}";
        ExpiresText = $"QR действует до {expiresAt.LocalDateTime:HH:mm}";
        Status = "Ожидание телефона...";
        IsRunning = true;

        _sessionTask = RunSessionAsync(server, lease, _sessionCts.Token);
    }

    [RelayCommand]
    public async Task StopAsync()
    {
        if (!IsRunning)
        {
            return;
        }

        Status = "Остановка...";
        _sessionCts?.Cancel();

        if (_sessionTask != null)
        {
            try
            {
                await _sessionTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task RunSessionAsync(
        PeerSyncServer server,
        IDisposable lease,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await server.AcceptOnceAsync(cancellationToken);
            lease.Dispose();

            if (!result.Success)
            {
                Status = "Не удалось подключиться: " + (result.Error ?? "неизвестная ошибка");
                return;
            }

            WeakReferenceMessenger.Default.Send(new DatabaseChangedMessage());
            Status = "Данные получены. Публикация в облако...";
            var publish = await _publisher.PublishAsync(cancellationToken);

            if (!publish.Attempted)
            {
                Status = "Синхронизация завершена.";
            }
            else if (publish.Success)
            {
                Status = "Синхронизировано и опубликовано в облако.";
            }
            else
            {
                Status = "Данные переданы, но в облако отправить не удалось: " + publish.Message;
            }
        }
        catch (OperationCanceledException)
        {
            Status = "Синхронизация отменена.";
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Peer host session failed");
            Status = "Ошибка синхронизации: " + ex.Message;
        }
        finally
        {
            lease.Dispose();
            IsRunning = false;
            PairingJson = string.Empty;
            EndpointText = string.Empty;
            ExpiresText = string.Empty;

            await server.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _sessionCts?.Dispose();
        _sessionCts = null;
    }

    private static string GetDeviceName()
    {
        var name = Environment.MachineName;
        return string.IsNullOrWhiteSpace(name) ? "Компьютер" : name;
    }
}
