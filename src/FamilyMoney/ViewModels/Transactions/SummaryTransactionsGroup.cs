using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyMoney.ViewModels;

public sealed class SummaryTransactionsGroup : ViewModelBase
{
    private readonly Dictionary<Guid, CategoryTransactionsGroupViewModel> _categoryGroupsCache = [];

    public bool IsDebet { get; init; }

    public decimal Sum { get; set; }

    public List<CategoryTransactionsGroupViewModel> Categories { get; private set; } = [];

    public CategoryTransactionsGroupViewModel TryAddCategory(Guid categoryId, Func<CategoryTransactionsGroupViewModel> factory)
    {
        if (!_categoryGroupsCache.TryGetValue(categoryId, out var category))
        {
            category = factory();
            _categoryGroupsCache.Add(categoryId, category);
            Categories.Add(category);
        }

        return category;
    }

    public void CalcPercentsAndSort()
    {
        foreach (var category in Categories)
        {
            category.Percent = Math.Round(category.Sum / Sum, 4, MidpointRounding.AwayFromZero);
            foreach (var subCategory in category.SubCategories)
            {
                subCategory.Percent = Math.Round(subCategory.Sum / Sum, 4, MidpointRounding.AwayFromZero);
                foreach (var transaction in subCategory.Transactions)
                {
                    transaction.Percent = Math.Round(transaction.Sum / Sum, 4, MidpointRounding.AwayFromZero);
                }
            }
        }

        Categories = [.. Categories.OrderByDescending(c => c.Percent)];
    }
}

