namespace FamilyMoney.Sync.Peer;

public sealed record PeerSyncProgress(
    string Phase,
    int Entities,
    int Images);

public sealed record PeerSyncResult(
    string PeerDeviceId,
    string PeerName,
    int ReceivedEntities,
    int ReceivedImages,
    int SentEntities,
    int SentImages,
    DateTime AppliedFromPeer,
    DateTime PeerAppliedFromUs)
{
    public string Message =>
        $"Получено записей: {ReceivedEntities}, фото: {ReceivedImages}; " +
        $"отправлено записей: {SentEntities}, фото: {SentImages}.";
}
