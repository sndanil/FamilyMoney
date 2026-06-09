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

    public bool IsHidden { get; set; }

    public bool IsExpanded { get; set; }

    public bool IsNotSummable { get; set; }
}

