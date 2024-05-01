using ReactiveUI;
using System.Collections.ObjectModel;

namespace FamilyMoney.ViewModels;

public sealed class TransactionsGroup: ViewModelBase
{
    private decimal _sum = 0;
    private ObservableCollection<CategoryTransactionsGroupViewModel> _categories = new();

    public decimal Sum
    {
        get => _sum;
        set => this.RaiseAndSetIfChanged(ref _sum, value);
    }

    public ObservableCollection<CategoryTransactionsGroupViewModel> Categories
    {
        get => _categories;
        set => this.RaiseAndSetIfChanged(ref _categories, value);
    }
}

public class BaseTransactionsGroupViewModel : ViewModelBase
{
    private decimal _sum = 0;
    private bool _isExpanded = false;

    public decimal Sum
    {
        get => _sum;
        set => this.RaiseAndSetIfChanged(ref _sum, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }
}

public class CategoryTransactionsGroupViewModel : BaseTransactionsGroupViewModel
{
    private BaseCategoryViewModel? _category;
    private ObservableCollection<SubCategoryTransactionsGroupViewModel> _subCategories = new ();

    public BaseCategoryViewModel? Category
    {
        get => _category;
        set => this.RaiseAndSetIfChanged(ref _category, value);
    }

    public ObservableCollection<SubCategoryTransactionsGroupViewModel> SubCategories
    {
        get => _subCategories;
        set => this.RaiseAndSetIfChanged(ref _subCategories, value);
    }
}

public class SubCategoryTransactionsGroupViewModel : BaseTransactionsGroupViewModel
{
    private BaseSubCategoryViewModel? _subCategory;
    private ObservableCollection<BaseTransactionViewModel> _transactions = new ();

    public BaseSubCategoryViewModel? SubCategory
    {
        get => _subCategory;
        set => this.RaiseAndSetIfChanged(ref _subCategory, value);
    }

    public ObservableCollection<BaseTransactionViewModel> Transactions
    {
        get => _transactions;
        set => this.RaiseAndSetIfChanged(ref _transactions, value);
    }
}
