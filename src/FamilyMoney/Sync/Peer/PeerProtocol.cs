namespace FamilyMoney.Sync.Peer;

public static class PeerProtocol
{
    public const int Version = 1;

    public const int DefaultMaxRecords = 2000;

    public const int MaxFrameBytes = 16 * 1024 * 1024;
}

public static class PeerErrorCodes
{
    public const string Protocol = "protocol";
    public const string BadToken = "badToken";
    public const string BadSyncId = "badSyncId";
    public const string Expired = "expired";
    public const string Busy = "busy";
    public const string Image = "image";
    public const string Internal = "internal";
}
