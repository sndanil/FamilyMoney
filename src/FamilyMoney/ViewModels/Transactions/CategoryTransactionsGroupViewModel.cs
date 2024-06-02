using System.Collections.Generic;

namespace FamilyMoney.ViewModels;

public class CategoryTransactionsGroupViewModel : BaseTransactionsGroupViewModel
{
    public BaseCategoryViewModel? Category { get; init; }

    public List<SubCategoryTransactionsGroupViewModel> SubCategories { get; } = [];
}
