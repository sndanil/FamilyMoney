namespace FamilyMoney.Sync;

public sealed class LocalSyncState
{
    public Guid DatabaseSyncId { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public long PublishedRevision { get; set; }

    public long AppliedRevision { get; set; }
}
