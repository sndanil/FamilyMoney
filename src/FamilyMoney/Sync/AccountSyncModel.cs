namespace FamilyMoney.Sync;

/// <summary>
/// Метаданные счёта для синхронизации. Баланс (Sum) не передаётся —
/// вычисляется локально из транзакций на каждом устройстве.
/// </summary>
internal sealed class AccountSyncModel
{
    public Guid Id { get; set; }

    public DateTime LastChange { get; set; }

    public DateTime? DeletedAt { get; set; }

    public Guid? ParentId { get; set; }

    public required string Name { get; set; }

    public int Order { get; set; }

    public bool IsGroup { get; set; }

    public bool IsNotSummable { get; set; }
}
