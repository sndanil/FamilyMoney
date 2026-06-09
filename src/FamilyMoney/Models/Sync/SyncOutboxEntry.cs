namespace FamilyMoney.Models.Sync;

public sealed class SyncOutboxEntry
{
    public int Id { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public DateTime LastChange { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DataJson { get; set; }
}
