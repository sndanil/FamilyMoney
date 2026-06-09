namespace FamilyMoney.Sync;

internal sealed class SyncImageMeta
{
    public Guid EntityId { get; set; }

    public DateTime LastChange { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string FileName { get; set; } = "image";
}
