using FamilyMoney.DataAccess;
using FamilyMoney.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace FamilyMoney.ViewModels;

public abstract class BaseTransactionViewModel : ViewModelBase
{
    private Guid _id;
    private AccountViewModel? _account;
    private decimal _sum = 0;
    private IList<AccountViewModel>? _flatAccounts;
    private IList<BaseCategoryViewModel>? _categories;
    private IList<BaseSubCategoryViewModel>? _subCategories;
    private string? _comment;
    private BaseCategoryViewModel? _category;
    private string? _subCategory;
    private DateTimeOffset? _date = DateTime.Today;
    private DateTime? _lastChange = DateTime.Now;

    public ReactiveCommand<Unit, BaseTransactionViewModel?> OkCommand { get; }

    public ReactiveCommand<Unit, BaseTransactionViewModel?> CancelCommand { get; }

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

    public decimal Sum
    {
        get => _sum;
        set => this.RaiseAndSetIfChanged(ref _sum, value);
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
    }

    public virtual void FillFrom(Transaction transaction, IRepository repository)
    {
        Id = transaction.Id;
        Sum = transaction.Sum;
        Date = transaction.Date;
        LastChange = transaction.LastChange;
        Comment = transaction.Comment;
        //Account = new AccountViewModel();
        //Account.FillFrom(repository.GetAccount(transaction.AccountId!.Value), repository);
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
