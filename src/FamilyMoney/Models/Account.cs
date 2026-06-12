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
}

