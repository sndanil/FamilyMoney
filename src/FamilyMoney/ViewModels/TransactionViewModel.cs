using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;

namespace FamilyMoney.ViewModels;

public class BaseTransactionViewModel : ViewModelBase
{
    private AccountViewModel? _account;
    private decimal _sum = 0;

    public AccountViewModel? Account
    {
        get => _account;
        set => this.RaiseAndSetIfChanged(ref _account, value);
    }

    public decimal Sum
    {
        get => _sum;
        set => this.RaiseAndSetIfChanged(ref _sum, value);
    }
}

public class TransactionViewModel : BaseTransactionViewModel
{
    private IList<AccountViewModel>? _flatAccounts;
    private IList<BaseCategoryViewModel>? _categories;
    private IList<BaseSubCategoryViewModel>? _subCategories;
    private string? _comment;
    private BaseCategoryViewModel? _category;
    private string? _subCategory;
    private DateTimeOffset? _date = DateTime.Today;
    private DateTime? _lastChanged = DateTime.Now;

    public ReactiveCommand<Unit, TransactionViewModel?> OkCommand { get; }

    public ReactiveCommand<Unit, TransactionViewModel?> CancelCommand { get; }

    public static Interaction<TransactionViewModel, TransactionViewModel?> ShowDialog { get; } = new();

    public TransactionViewModel()
    {
        var canExecute = this.WhenAnyValue(x => x.Sum, x => x.Account, (sum, account) => sum != 0 && account != null);
        OkCommand = ReactiveCommand.Create(() =>
        {
            return (TransactionViewModel?)this;
        },
        canExecute);

        CancelCommand = ReactiveCommand.Create(() =>
        {
            return (TransactionViewModel?)null;
        });
    }

    public IList<AccountViewModel>? FlatAccounts
    {
        get => _flatAccounts;
        set => this.RaiseAndSetIfChanged(ref _flatAccounts, value);
    }

    public IList<BaseCategoryViewModel>? Categories
    {
        get => _categories;
        set => this.RaiseAndSetIfChanged(ref _categories, value);
    }

    public IList<BaseSubCategoryViewModel>? SubCategories
    {
        get => _subCategories;
        set => this.RaiseAndSetIfChanged(ref _subCategories, value);
    }

    public BaseCategoryViewModel? Category
    {
        get => _category;
        set => this.RaiseAndSetIfChanged(ref _category, value);
    }

    public string? Comment
    {
        get => _comment;
        set => this.RaiseAndSetIfChanged(ref _comment, value);
    }

    public string? SubCategory
    {
        get => _subCategory;
        set => this.RaiseAndSetIfChanged(ref _subCategory, value);
    }

    public DateTimeOffset? Date
    {
        get => _date;
        set => this.RaiseAndSetIfChanged(ref _date, value);
    }

    public DateTime? LastChanged
    {
        get => _lastChanged;
        set => this.RaiseAndSetIfChanged(ref _lastChanged, value);
    }
}

public class DebetTransactionViewModel : TransactionViewModel
{
}

public class CreditTransactionViewModel : TransactionViewModel
{
}

public class TransferTransactionViewModel : DebetTransactionViewModel
{
}
