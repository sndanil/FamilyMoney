using FamilyMoney.DataAccess;
using ReactiveUI;
using System;
using System.Windows.Input;

namespace FamilyMoney.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private int _leftSideWidth = 400;
    private bool _isPaneOpen = false;

    private readonly PeriodViewModel _period;
    private readonly AccountsViewModel _accountsViewModel;
    private readonly TransactionsViewModel _transactionsViewModel;

    public ICommand TriggerPaneCommand { get; }

    public MainWindowViewModel(IRepository repository, AccountsViewModel accounts, TransactionsViewModel transactionsViewModel)
    {
        TriggerPaneCommand = ReactiveCommand.Create(() => IsPaneOpen = !IsPaneOpen);

        _accountsViewModel = accounts;
        _transactionsViewModel = transactionsViewModel;
        _transactionsViewModel.MainWindowViewModel = this;

        _period = new PeriodViewModel 
        { 
            From = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
            To = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1).AddDays(-1),
            PeriodType = PeriodType.Month 
        };
        _period.PropertyChanged += (e, a) => this.RaisePropertyChanged(nameof(Period));
    }

    public int LeftSideWidth 
    { 
        get => _leftSideWidth; 
        set => this.RaiseAndSetIfChanged(ref _leftSideWidth, value); 
    }

    public bool IsPaneOpen
    {
        get => _isPaneOpen;
        set => this.RaiseAndSetIfChanged(ref _isPaneOpen, value);
    }

    public PeriodViewModel Period
    {
        get => _period;
    }

    public AccountsViewModel Accounts
    {
        get => _accountsViewModel;
    }

    public TransactionsViewModel Transactions
    {
        get => _transactionsViewModel;
    }

}
