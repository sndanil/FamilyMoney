using FamilyMoney.DataAccess;
using FamilyMoney.Models.Sync;
using FamilyMoney.State;
using Microsoft.Extensions.Logging;

namespace FamilyMoney.Sync.Peer;

/// <summary>
/// Клиентская часть peer-сессии: тянет данные сервера и отправляет свои.
/// В MVP клиентом является телефон.
/// </summary>
public sealed class PeerSyncClientEngine
{
    private readonly IRepository _repository;
    private readonly PeerSyncStateStore _stateStore;
    private readonly Guid _syncId;
    private readonly string _deviceId;
    private readonly string _deviceName;
    private readonly int _maxRecords;
    private readonly ILogger<PeerSyncClientEngine>? _logger;
    private readonly IAccountLocalSettingsStore? _localSettings;

    public PeerSyncClientEngine(
        Guid syncId,
        string deviceId,
        string deviceName,
        IRepository repository,
        PeerSyncStateStore stateStore,
        int maxRecords = PeerProtocol.DefaultMaxRecords,
        ILogger<PeerSyncClientEngine>? logger = null,
        IAccountLocalSettingsStore? localSettings = null)
    {
        _syncId = syncId;
        _deviceId = deviceId;
        _deviceName = deviceName;
        _repository = repository;
        _stateStore = stateStore;
        _maxRecords = Math.Clamp(maxRecords, 1, 5000);
        _logger = logger;
        _localSettings = localSettings;
    }

    public async Task<PeerSyncResult> SyncAsync(
        IPeerTransport transport,
        string pairingToken,
        IProgress<PeerSyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var ack = await transport.HelloAsync(
            new PeerHello
            {
                Protocol = PeerProtocol.Version,
                SyncId = _syncId,
                DeviceId = _deviceId,
                DeviceName = _deviceName,
                Token = pairingToken,
            },
            cancellationToken);

        if (ack.Protocol != PeerProtocol.Version)
        {
            throw new PeerSyncException(
                PeerErrorCodes.Protocol,
                "Компьютер использует несовместимую версию протокола.");
        }

        if (ack.SyncId != _syncId)
        {
            throw new PeerSyncException(
                PeerErrorCodes.BadSyncId,
                "Компьютер ответил для другой базы.");
        }

        var state = _stateStore.Load(_syncId, ack.DeviceId);
        state.PeerName = ack.DeviceName;
        state.PeerAppliedFromUs = ack.ServerAppliedFromClient;
        _stateStore.Save(state);

        // Переносим видимость счетов с компьютера: скрываем те, что скрыты там.
        // Счета, у которых на телефоне уже есть своя настройка, не трогаем.
        if (ack.HiddenAccountIds.Count > 0)
        {
            _localSettings?.ApplyPeerHidden(ack.HiddenAccountIds);
        }

        var hadLocalChanges = _repository.GetPendingSyncChanges().HasChanges;
        var firstSync = state.AppliedFromPeer == DateTime.MinValue;

        var receivedEntities = 0;
        var receivedImages = 0;
        var pageSince = state.AppliedFromPeer;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await transport.PullAsync(
                new PeerPullRequest
                {
                    Since = pageSince,
                    MaxRecords = _maxRecords,
                },
                cancellationToken);

            _repository.ApplySyncDelta(response.Delta);

            var images = await _repository.ApplySyncedImagesAsync(
                response.Delta.Images,
                transport.DownloadImageAsync,
                cancellationToken);

            if (images.HasFailures)
            {
                throw new PeerSyncException(
                    PeerErrorCodes.Image,
                    $"Не удалось получить изображений: {images.Failed.Count}.");
            }

            receivedEntities += PeerDeltaMetrics.EntityCount(response.Delta);
            receivedImages += PeerDeltaMetrics.ImageCount(response.Delta);

            if (response.Watermark < state.AppliedFromPeer)
            {
                throw new PeerSyncException(
                    PeerErrorCodes.Internal,
                    "Сервер вернул некорректную отметку синхронизации.");
            }

            // Пустая страница возвращает Watermark = курсор, но это не значит,
            // что данные до курсора применены: состояние не двигаем.
            if ((PeerDeltaMetrics.EntityCount(response.Delta) > 0
                    || PeerDeltaMetrics.ImageCount(response.Delta) > 0)
                && response.Watermark > state.AppliedFromPeer)
            {
                state.AppliedFromPeer = response.Watermark;
            }

            state.LastSyncAt = DateTime.UtcNow;
            _stateStore.Save(state);

            progress?.Report(new PeerSyncProgress("pull", receivedEntities, receivedImages));

            if (!response.HasMore)
            {
                break;
            }

            if (response.Next <= pageSince)
            {
                throw new PeerSyncException(
                    PeerErrorCodes.Internal,
                    "Сервер не продвинул страницу синхронизации.");
            }

            pageSince = response.Next;
        }

        // Первая синхронизация пустой базы: всё полученное уже есть у пира,
        // отправлять его обратно не нужно. Иначе пустой телефон «выгружает»
        // ровно то, что только что скачал.
        if (firstSync && !hadLocalChanges)
        {
            state.PushFrom = NextCursor(state.AppliedFromPeer);
            state.LastSyncAt = DateTime.UtcNow;
            _stateStore.Save(state);
        }

        var sentEntities = 0;
        var sentImages = 0;
        var pushSince = ack.ServerAppliedFromClient > state.PushFrom
            ? NextCursor(ack.ServerAppliedFromClient)
            : state.PushFrom;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = _repository.GetSyncChangesSince(pushSince, _maxRecords);
            if (!batch.Changes.HasChanges)
            {
                break;
            }

            var delta = SyncDeltaBuilder.Build(batch.Changes.Entities, 0, _deviceId);
            SyncDeltaBuilder.AddImages(delta, batch.Changes.Images);

            var localNext = PeerDeltaMetrics.MaxLastChange(delta);
            var pushAck = await transport.PushAsync(
                new PeerPushRequest
                {
                    SyncId = _syncId,
                    DeviceId = _deviceId,
                    DeviceName = _deviceName,
                    Delta = delta,
                },
                (image, _) => Task.FromResult(_repository.TryGetImage(image.EntityId)),
                cancellationToken);

            if (pushAck.ServerAppliedFromClient < localNext)
            {
                throw new PeerSyncException(
                    PeerErrorCodes.Internal,
                    "Сервер подтвердил не все отправленные изменения.");
            }

            state.PeerAppliedFromUs = pushAck.ServerAppliedFromClient;
            state.PushFrom = pushAck.ServerAppliedFromClient > state.PushFrom
                ? NextCursor(pushAck.ServerAppliedFromClient)
                : state.PushFrom;
            state.LastSyncAt = DateTime.UtcNow;
            _stateStore.Save(state);

            sentEntities += PeerDeltaMetrics.EntityCount(delta);
            sentImages += PeerDeltaMetrics.ImageCount(delta);

            progress?.Report(new PeerSyncProgress("push", sentEntities, sentImages));

            if (!batch.HasMore)
            {
                break;
            }

            if (pushAck.Next <= pushSince)
            {
                throw new PeerSyncException(
                    PeerErrorCodes.Internal,
                    "Сервер не продвинул страницу принятых изменений.");
            }

            pushSince = pushAck.Next;
        }

        state.LastSyncAt = DateTime.UtcNow;
        _stateStore.Save(state);

        _logger?.LogInformation(
            "Peer sync completed with {PeerDeviceId}: received {ReceivedEntities}/{ReceivedImages}, sent {SentEntities}/{SentImages}",
            ack.DeviceId,
            receivedEntities,
            receivedImages,
            sentEntities,
            sentImages);

        return new PeerSyncResult(
            ack.DeviceId,
            ack.DeviceName,
            receivedEntities,
            receivedImages,
            sentEntities,
            sentImages,
            state.AppliedFromPeer,
            state.PeerAppliedFromUs);
    }

    /// <summary>Следующая миллисекунда, чтобы не переотправлять записи на самой отметке.</summary>
    private static DateTime NextCursor(DateTime value) =>
        value == DateTime.MaxValue ? value : value.AddMilliseconds(1);
}
