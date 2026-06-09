namespace FamilyMoney.Sync;

public sealed class SyncDelta
{
    public long Revision { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public List<SyncEntityRecord> Accounts { get; set; } = [];

    public List<SyncEntityRecord> Categories { get; set; } = [];

    public List<SyncEntityRecord> SubCategories { get; set; } = [];

    public List<SyncEntityRecord> Transactions { get; set; } = [];

    public List<SyncImageRecord> Images { get; set; } = [];
}
