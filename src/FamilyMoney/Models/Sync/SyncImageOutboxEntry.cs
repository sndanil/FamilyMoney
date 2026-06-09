namespace FamilyMoney.Models.Sync;

public sealed class SyncImageOutboxEntry
{
    public int Id { get; set; }

    public Guid EntityId { get; set; }

    public DateTime LastChange { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string FileName { get; set; } = "image";
}
