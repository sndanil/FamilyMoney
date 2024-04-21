using ReactiveUI;
using System;

namespace FamilyMoney.ViewModels;

public class BaseTransactionViewModel : ViewModelBase
{
    private AccountViewModel? _account;
    private decimal _amount = 0;

    public AccountViewModel? Account
    {
        get => _account;
        set => this.RaiseAndSetIfChanged(ref _account, value);
    }

    public decimal Amount
    {
        get => _amount;
        set => this.RaiseAndSetIfChanged(ref _amount, value);
    }
}

public class TransactionViewModel : BaseTransactionViewModel
{
    private string? _comment;
    private DateTime? _date;
    private DateTime? _lastChanged;

    public string? Comment
    {
        get => _comment;
        set => this.RaiseAndSetIfChanged(ref _comment, value);
    }

    public DateTime? Date
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
    private DebetCategoryViewModel? _category;

    public DebetCategoryViewModel? Category
    {
        get => _category;
        set => this.RaiseAndSetIfChanged(ref _category, value);
    }
}

public class CreditTransactionViewModel : TransactionViewModel
{
    private CreditCategoryViewModel? _category;

    public CreditCategoryViewModel? Category
    {
        get => _category;
        set => this.RaiseAndSetIfChanged(ref _category, value);
    }
}

public class MoveTransactionViewModel : DebetTransactionViewModel
{
    private CreditCategoryViewModel? _creditCategory;

    public CreditCategoryViewModel? CreditCategory
    {
        get => _creditCategory;
        set => this.RaiseAndSetIfChanged(ref _creditCategory, value);
    }
}
