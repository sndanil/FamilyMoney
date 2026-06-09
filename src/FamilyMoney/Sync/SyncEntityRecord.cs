namespace FamilyMoney.Sync;

public sealed class SyncEntityRecord
{
    public Guid Id { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public DateTime LastChange { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DataJson { get; set; }
}
