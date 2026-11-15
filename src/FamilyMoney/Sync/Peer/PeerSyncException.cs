namespace FamilyMoney.Sync.Peer;

public sealed class PeerSyncException : Exception
{
    public PeerSyncException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
