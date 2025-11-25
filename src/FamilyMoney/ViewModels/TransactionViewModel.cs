using FamilyMoney.DataAccess;
using FamilyMoney.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Windows.Input;

namespace FamilyMoney.ViewModels;

public abstract class BaseTransactionViewModel : ViewModelBase
{
    private Guid _id;
    private AccountViewModel? _account;
    private AccountViewModel? _toAccount;
    private bool _isTransfer = false;
    private decimal _sum = 0;
    private decimal _toSum = 0;
    private IList<AccountViewModel>? _flatAccounts;
    private IList<BaseCategoryViewModel>? _categories;
    private IList<BaseSubCategoryViewModel>? _subCategories;
    private string? _comment;
    private IList<string> _comments = [];
    private BaseCategoryViewModel? _category;
    private BaseSubCategoryViewModel? _subCategory;
    private string? _subCategoryText;
    private DateTime? _date = DateTime.Today;
    private DateTime? _lastChange = DateTime.Now;

    public ReactiveCommand<Unit, BaseTransactionViewModel?> OkCommand { get; }

    public ReactiveCommand<Unit, BaseTransactionViewModel?> CancelCommand { get; }

    public ICommand CommentCommand { get; }

    public ICommand PrevDayCommand { get; }

    public ICommand NextDayCommand { get; }

    public static Interaction<BaseTransactionViewModel, BaseTransactionViewModel?> ShowDialog { get; } = new();

    public Guid Id
    {
        get => _id;
        set => this.RaiseAndSetIfChanged(ref _id, value);
    }

    public AccountViewModel? Account
    {
        get => _account;
        set => this.RaiseAndSetIfChanged(ref _account, value);
    }

    public AccountViewModel? ToAccount
    {
        get => _toAccount;
        set => this.RaiseAndSetIfChanged(ref _toAccount, value);
    }

    public bool IsTransfer
    {
        get => _isTransfer;
        set => this.RaiseAndSetIfChanged(ref _isTransfer, value);
    }

    public decimal Sum
    {
        get => _sum;
        set => this.RaiseAndSetIfChanged(ref _sum, value);
    }

    public decimal ToSum
    {
        get => _toSum;
        set => this.RaiseAndSetIfChanged(ref _toSum, value);
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

    public IList<string> Comments
    {
        get => _comments;
        set => this.RaiseAndSetIfChanged(ref _comments, value);
    }

    public string? SubCategoryText
    {
        get => _subCategoryText;
        set => this.RaiseAndSetIfChanged(ref _subCategoryText, value);
    }

    public BaseSubCategoryViewModel? SubCategory
    {
        get => _subCategory;
        set => this.RaiseAndSetIfChanged(ref _subCategory, value);
    }

    public DateTime? Date
    {
        get => _date;
        set => this.RaiseAndSetIfChanged(ref _date, value);
    }

    public DateTime? LastChange
    {
        get => _lastChange;
        set => this.RaiseAndSetIfChanged(ref _lastChange, value);
    }

    public BaseTransactionViewModel()
    {
        var canExecute = this.WhenAnyValue(x => x.Sum, x => x.Account, (sum, account) => sum != 0 && account != null);
        OkCommand = ReactiveCommand.Create(() =>
        {
            return (BaseTransactionViewModel?)this;
        },
        canExecute);

        CancelCommand = ReactiveCommand.Create(() =>
        {
            return (BaseTransactionViewModel?)null;
        });

        CommentCommand = ReactiveCommand.Create((string comment) =>
        {
            this.Comment += comment;
        });

        PrevDayCommand = ReactiveCommand.Create(() =>
        {
            Date = Date!.Value.AddDays(-1);
        });

        NextDayCommand = ReactiveCommand.Create(() =>
        {
            Date = Date!.Value.AddDays(1);
        });
    }

    public virtual void FillFrom(Transaction transaction, IRepository repository)
    {
        Id = transaction.Id;
        Sum = transaction.Sum;
        Date = transaction.Date;
        LastChange = transaction.LastChange;
        Comment = transaction.Comment;
    }
}

public class DebetTransactionViewModel : BaseTransactionViewModel
{
}

public class CreditTransactionViewModel : BaseTransactionViewModel
{
}

public class TransferTransactionViewModel : DebetTransactionViewModel
{
}
