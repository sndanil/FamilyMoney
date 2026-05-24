using System;
using System.Collections.Generic;

namespace FamilyMoney.ViewModels;

public class TransactionRowViewModel : BaseTransactionsGroupViewModel
{
    public Guid Id { get; init; }

    public string? Comment { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public bool HasTags => Tags.Count > 0;

    public DateTime Date { get; init; }

    public DateTime LastChange { get; init; }

    public BaseCategoryViewModel? Category { get; init; }

    public BaseSubCategoryViewModel? SubCategory { get; init; }
}