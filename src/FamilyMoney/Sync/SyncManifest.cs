namespace FamilyMoney.Sync;

public sealed class SyncManifest
{
    public long Revision { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string UpdatedByDevice { get; set; } = string.Empty;

    public long? SnapshotRevision { get; set; }

    public string? SnapshotKey { get; set; }
}
