using System.Collections.Generic;

namespace FamilyMoney.ViewModels;

public class SubCategoryTransactionsGroupViewModel : BaseTransactionsGroupViewModel
{
    public BaseSubCategoryViewModel? SubCategory { get; init; }

    public string DisplayName => SubCategory?.Name ?? "Без подкатегории";

    public List<TransactionRowViewModel> Transactions { get; } = [];
}