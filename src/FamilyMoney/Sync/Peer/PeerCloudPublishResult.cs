namespace FamilyMoney.Sync.Peer;

public sealed record PeerCloudPublishResult(
    bool Attempted,
    bool Success,
    string Message,
    SyncResult? SyncResult = null)
{
    public static PeerCloudPublishResult Skipped(string message) =>
        new(false, true, message);
}
