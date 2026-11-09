using System;

namespace FamilyMoney.Models;

public sealed class Account : ISyncable
{
    public required Guid Id { get; set; }

    public DateTime LastChange { get; set; }

    public DateTime? DeletedAt { get; set; }

    public Guid? ParentId { get; set; }

    public required string Name { get; set; }

    public int Order { get; set; }

    public bool IsGroup { get; set; }

    public decimal Sum { get; set; }

    // Sum вычисляется локально из транзакций (RecalculateAccountBalances) и не синхронизируется.

    // Видимость и свёрнутость хранятся локально для каждого устройства
    // (IAccountLocalSettingsStore) и не синхронизируются между устройствами.

    public bool IsNotSummable { get; set; }

    /// <summary>Дата окончания вклада. Пусто у обычных счетов и групп.</summary>
    public DateTime? EndsOn { get; set; }

    /// <summary>
    /// Ставка в процентах так, как её указывает пользователь: 16,5 — это 16,5%.
    /// Пусто, если ставка не задана; 0 — настоящая нулевая ставка.
    /// </summary>
    public decimal? AnnualRate { get; set; }
}

