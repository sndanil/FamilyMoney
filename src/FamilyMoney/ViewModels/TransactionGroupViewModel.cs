using FamilyMoney.Messages;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FamilyMoney.ViewModels;

public sealed class TransactionsGroup: ViewModelBase
{
    private bool _isDebet = false;
    private decimal _sum = 0;
    private ObservableCollection<CategoryTransactionsGroupViewModel> _categories = new();

    public bool IsDebet
    {
        get => _isDebet;
        set => this.RaiseAndSetIfChanged(ref _isDebet, value);
    }

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
    private bool _isDebet = false;
    private decimal _sum = 0;
    private decimal _percent = 0;
    private bool _isExpanded = false;
    private bool _isSelected = false;

    public bool IsDebet
    {
        get => _isDebet;
        set => this.RaiseAndSetIfChanged(ref _isDebet, value);
    }

    public decimal Sum
    {
        get => _sum;
        set => this.RaiseAndSetIfChanged(ref _sum, value);
    }

    public decimal Percent
    {
        get => _percent;
        set => this.RaiseAndSetIfChanged(ref _percent, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    public ICommand ToggleExpand { get; }

    public ICommand SelectCommand { get; }

    public BaseTransactionsGroupViewModel()
    {
        ToggleExpand = ReactiveCommand.Create(() =>
        {
            IsExpanded = !IsExpanded;
            MessageBus.Current.SendMessage(new TransactionGroupExpandMessage { Element = this });
        });

        SelectCommand = ReactiveCommand.Create(() =>
        {
            MessageBus.Current.SendMessage(new TransactionGroupSelectMessage { Element = this });
        });
    }
}

public class TransactionGroupViewModel : BaseTransactionsGroupViewModel
{
    private Guid _id;
    private string? _comment;
    private DateTime _date;

    public Guid Id
    {
        get => _id;
        set => this.RaiseAndSetIfChanged(ref _id, value);
    }

    public string? Comment
    {
        get => _comment;
        set => this.RaiseAndSetIfChanged(ref _comment, value);
    }

    public DateTime Date
    {
        get => _date;
        set => this.RaiseAndSetIfChanged(ref _date, value);
    }

    public ICommand EditCommand { get; }

    public TransactionGroupViewModel()
    {
        EditCommand = ReactiveCommand.Create(() =>
        {
            MessageBus.Current.SendMessage(new TransactionGroupEditMessage { Element = this });
        });
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
    private ObservableCollection<TransactionGroupViewModel> _transactions = new ();

    public BaseSubCategoryViewModel? SubCategory
    {
        get => _subCategory;
        set => this.RaiseAndSetIfChanged(ref _subCategory, value);
    }

    public ObservableCollection<TransactionGroupViewModel> Transactions
    {
        get => _transactions;
        set => this.RaiseAndSetIfChanged(ref _transactions, value);
    }
}
