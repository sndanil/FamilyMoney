namespace FamilyMoney.Sync.Peer;

public sealed class PeerHello
{
    public int Protocol { get; set; } = PeerProtocol.Version;

    public Guid SyncId { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;
}

public sealed class PeerHelloAck
{
    public int Protocol { get; set; } = PeerProtocol.Version;

    public Guid SyncId { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public DateTime ServerAppliedFromClient { get; set; }

    public DateTime ServerNow { get; set; }
}

public sealed class PeerPullRequest
{
    public DateTime Since { get; set; }

    public int MaxRecords { get; set; } = PeerProtocol.DefaultMaxRecords;
}

public sealed class PeerChangesResponse
{
    public SyncDelta Delta { get; set; } = new();

    /// <summary>Курсор следующей страницы.</summary>
    public DateTime Next { get; set; }

    /// <summary>Максимальный LastChange в этой порции; сохраняется в состоянии.</summary>
    public DateTime Watermark { get; set; }

    public bool HasMore { get; set; }
}

public sealed class PeerPushRequest
{
    public Guid SyncId { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public SyncDelta Delta { get; set; } = new();
}

public sealed class PeerPushAck
{
    /// <summary>Максимальный LastChange, принятый сервером.</summary>
    public DateTime ServerAppliedFromClient { get; set; }

    /// <summary>Курсор следующей страницы отправки.</summary>
    public DateTime Next { get; set; }
}
