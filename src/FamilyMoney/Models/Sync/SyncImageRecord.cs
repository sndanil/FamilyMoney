namespace FamilyMoney.Models.Sync;

public sealed class SyncImageRecord
{
    public Guid EntityId { get; set; }

    public DateTime LastChange { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? FileName { get; set; }
}
