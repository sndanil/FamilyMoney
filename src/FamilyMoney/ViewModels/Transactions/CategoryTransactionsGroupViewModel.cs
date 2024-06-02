using System;
using System.Collections.Generic;

namespace FamilyMoney.ViewModels;

public class CategoryTransactionsGroupViewModel : BaseTransactionsGroupViewModel
{
    private readonly Dictionary<Guid, SubCategoryTransactionsGroupViewModel> _subCategoryGroupsCache = [];

    public BaseCategoryViewModel? Category { get; init; }

    public List<SubCategoryTransactionsGroupViewModel> SubCategories { get; } = [];

    public SubCategoryTransactionsGroupViewModel TryAddSubCategory(Guid subCategoryId, Func<SubCategoryTransactionsGroupViewModel> factory)
    {
        if (!_subCategoryGroupsCache.TryGetValue(subCategoryId, out var subCategory))
        {
            subCategory = factory();
            _subCategoryGroupsCache.Add(subCategoryId, subCategory);
            SubCategories.Add(subCategory);
        }

        return subCategory;
    }
}
