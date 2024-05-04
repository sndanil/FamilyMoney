using System;

namespace FamilyMoney.Models;

public abstract class Transaction
{
    public required Guid Id { get; set; }

    public Guid? AccountId { get; set; }

    public Guid? CategoryId { get; set; }

    public Guid? SubCategoryId { get; set; }

    public decimal Sum { get; set; }

    public string? Comment { get; set; }

    public DateTime Date { get; set; }

    public DateTime LastChange { get; set; }

    public string[]? Tags { get; set; }
}

public sealed class DebetTransaction: Transaction 
{
}

public sealed class CreditTransaction : Transaction
{
}

public sealed class TransferTransaction : Transaction
{
    public Guid? ToAccountId { get; set; }

    public decimal ToSum { get; set; }
}
