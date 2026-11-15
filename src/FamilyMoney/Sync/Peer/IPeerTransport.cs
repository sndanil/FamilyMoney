using FamilyMoney.Models.Sync;

namespace FamilyMoney.Sync.Peer;

/// <summary>
/// Абстракция peer-сессии. В тестах — in-memory, в приложении — TCP/TLS.
/// </summary>
public interface IPeerTransport
{
    Task<PeerHelloAck> HelloAsync(
        PeerHello hello,
        CancellationToken cancellationToken = default);

    Task<PeerChangesResponse> PullAsync(
        PeerPullRequest request,
        CancellationToken cancellationToken = default);

    Task<PeerPushAck> PushAsync(
        PeerPushRequest request,
        Func<SyncImageRecord, CancellationToken, Task<Stream?>> openImage,
        CancellationToken cancellationToken = default);

    Task<Stream?> DownloadImageAsync(
        SyncImageRecord image,
        CancellationToken cancellationToken = default);
}
