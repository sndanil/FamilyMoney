namespace FamilyMoney.Sync.Peer;

public sealed record PeerServerEndpoint(
    string Host,
    int Port,
    string Fingerprint)
{
    public string Address => $"{Host}:{Port}";
}

public sealed record PeerSessionResult(bool Success, string? Error)
{
    public static PeerSessionResult Completed() => new(true, null);
}
