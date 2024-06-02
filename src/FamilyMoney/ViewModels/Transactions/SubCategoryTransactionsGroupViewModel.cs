using System.Collections.Generic;

namespace FamilyMoney.ViewModels;

public class SubCategoryTransactionsGroupViewModel : BaseTransactionsGroupViewModel
{
    public BaseSubCategoryViewModel? SubCategory { get; init; }

    public List<TransactionRowViewModel> Transactions { get; } = [];
}