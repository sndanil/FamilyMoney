using System;

namespace FamilyMoney.Models;

public abstract class Category : ISyncable
{
    public required Guid Id { get; set; }

    public required string Name { get; set; }

    public bool IsHidden { get; set; }

    public DateTime LastChange { get; set; }

    public DateTime? DeletedAt { get; set; }

    /// <summary>material:CartOutline, letter:П или pack:banks/sber. null — картинка или fallback.</summary>
    public string? Icon { get; set; }

    /// <summary>#RRGGBB; null — акцентный цвет.</summary>
    public string? IconColor { get; set; }
}

public sealed class DebetCategory: Category
{
}

public sealed class CreditCategory: Category
{
}

public sealed class TransferCategory: Category
{
}
