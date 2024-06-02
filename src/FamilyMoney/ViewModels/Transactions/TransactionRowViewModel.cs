using System;

namespace FamilyMoney.ViewModels;

public class TransactionRowViewModel : BaseTransactionsGroupViewModel
{
    public Guid Id { get; init; }

    public string? Comment { get; init; }

    public DateTime Date { get; init; }

    public DateTime LastChange { get; init; }

    public BaseCategoryViewModel? Category { get; init; }

    public BaseSubCategoryViewModel? SubCategory { get; init; }
}