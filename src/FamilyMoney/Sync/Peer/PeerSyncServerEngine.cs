using FamilyMoney.DataAccess;
using FamilyMoney.Models.Sync;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace FamilyMoney.Sync.Peer;

/// <summary>
/// Серверная часть peer-сессии: проверка пейринга и обмен изменениями.
/// Сетевой слой (TLS/TCP) вызывает эти методы; сам движок не знает о сокетах.
/// </summary>
public sealed class PeerSyncServerEngine
{
    private readonly IRepository _repository;
    private readonly PeerSyncStateStore _stateStore;
    private readonly Guid _syncId;
    private readonly string _deviceId;
    private readonly string _deviceName;
    private readonly string _pairingToken;
    private readonly DateTimeOffset _expiresAt;
    private readonly bool _publishReceivedChanges;
    private readonly FamilyMoney.State.IAccountLocalSettingsStore? _localSettings;
    private readonly ILogger<PeerSyncServerEngine>? _logger;

    public PeerSyncServerEngine(
        Guid syncId,
        string deviceId,
        string deviceName,
        string pairingToken,
        DateTimeOffset expiresAt,
        IRepository repository,
        PeerSyncStateStore stateStore,
        ILogger<PeerSyncServerEngine>? logger = null,
        bool publishReceivedChanges = false,
        FamilyMoney.State.IAccountLocalSettingsStore? localSettings = null)
    {
        _syncId = syncId;
        _deviceId = deviceId;
        _deviceName = deviceName;
        _pairingToken = pairingToken;
        _expiresAt = expiresAt;
        _repository = repository;
        _stateStore = stateStore;
        _logger = logger;
        _publishReceivedChanges = publishReceivedChanges;
        _localSettings = localSettings;
    }

    public PeerHelloAck Hello(PeerHello hello)
    {
        if (hello.Protocol != PeerProtocol.Version)
        {
            throw new PeerSyncException(
                PeerErrorCodes.Protocol,
                "Версия протокола Wi-Fi-синхронизации не поддерживается.");
        }

        if (hello.SyncId != _syncId)
        {
            throw new PeerSyncException(
                PeerErrorCodes.BadSyncId,
                "QR-код относится к другой базе.");
        }

        if (string.IsNullOrWhiteSpace(hello.DeviceId))
        {
            throw new PeerSyncException(
                PeerErrorCodes.Protocol,
                "Не указан идентификатор устройства.");
        }

        if (DateTimeOffset.UtcNow > _expiresAt)
        {
            throw new PeerSyncException(
                PeerErrorCodes.Expired,
                "Срок действия QR-кода истёк. Покажите новый QR на компьютере.");
        }

        if (!TokenEquals(hello.Token))
        {
            throw new PeerSyncException(
                PeerErrorCodes.BadToken,
                "Неверный код сопряжения. Покажите новый QR на компьютере.");
        }

        var state = _stateStore.Load(_syncId, hello.DeviceId);
        state.PeerName = hello.DeviceName;
        _stateStore.Save(state);

        _logger?.LogInformation(
            "Peer hello accepted: {PeerDeviceId} ({PeerName})",
            hello.DeviceId,
            hello.DeviceName);

        return new PeerHelloAck
        {
            Protocol = PeerProtocol.Version,
            SyncId = _syncId,
            DeviceId = _deviceId,
            DeviceName = _deviceName,
            ServerAppliedFromClient = state.AppliedFromPeer,
            ServerNow = DateTime.UtcNow,
            HiddenAccountIds = _localSettings?.GetHiddenAccountIds().ToList() ?? [],
        };
    }

    public PeerChangesResponse Pull(PeerPullRequest request)
    {
        var maxRecords = Math.Clamp(
            request.MaxRecords <= 0 ? PeerProtocol.DefaultMaxRecords : request.MaxRecords,
            1,
            5000);

        var batch = _repository.GetSyncChangesSince(request.Since, maxRecords);
        var delta = SyncDeltaBuilder.Build(batch.Changes.Entities, 0, _deviceId);
        SyncDeltaBuilder.AddImages(delta, batch.Changes.Images);

        return new PeerChangesResponse
        {
            Delta = delta,
            Next = batch.Next,
            Watermark = batch.Watermark,
            HasMore = batch.HasMore,
        };
    }

    public async Task<PeerPushAck> PushAsync(
        PeerPushRequest request,
        Func<SyncImageRecord, CancellationToken, Task<Stream?>> openImage,
        CancellationToken cancellationToken = default)
    {
        if (request.SyncId != _syncId)
        {
            throw new PeerSyncException(
                PeerErrorCodes.BadSyncId,
                "Пакет относится к другой базе.");
        }

        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            throw new PeerSyncException(
                PeerErrorCodes.Protocol,
                "Не указан идентификатор устройства.");
        }

        _repository.ApplyPeerDelta(request.Delta, _publishReceivedChanges);

        var images = _publishReceivedChanges
            ? await _repository.ApplyPeerImagesAsync(
                request.Delta.Images,
                openImage,
                enqueueForPublish: true,
                cancellationToken: cancellationToken)
            : await _repository.ApplySyncedImagesAsync(
                request.Delta.Images,
                openImage,
                cancellationToken);

        if (images.HasFailures)
        {
            throw new PeerSyncException(
                PeerErrorCodes.Image,
                $"Не удалось принять изображений: {images.Failed.Count}.");
        }

        var state = _stateStore.Load(_syncId, request.DeviceId);
        var watermark = PeerDeltaMetrics.MaxLastChange(request.Delta);
        if (watermark > state.AppliedFromPeer)
        {
            state.AppliedFromPeer = watermark;
        }

        state.PeerName = string.IsNullOrWhiteSpace(request.DeviceName)
            ? state.PeerName
            : request.DeviceName;
        state.LastSyncAt = DateTime.UtcNow;
        _stateStore.Save(state);

        return new PeerPushAck
        {
            ServerAppliedFromClient = state.AppliedFromPeer,
            Next = PeerDeltaMetrics.NextWatermark(request.Delta),
        };
    }

    public Task<Stream?> DownloadImageAsync(
        SyncImageRecord image,
        CancellationToken cancellationToken = default)
    {
        if (image.DeletedAt != null)
        {
            return Task.FromResult<Stream?>(null);
        }

        return Task.FromResult(_repository.TryGetImage(image.EntityId));
    }

    private bool TokenEquals(string? provided)
    {
        if (string.IsNullOrEmpty(provided))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(_pairingToken);
        var actual = Encoding.UTF8.GetBytes(provided);
        return expected.Length == actual.Length
            && CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
