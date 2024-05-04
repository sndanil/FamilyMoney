using FamilyMoney.DataAccess;
using FamilyMoney.State;
using ReactiveUI;
using System;
using System.Reactive.Concurrency;
using System.Windows.Input;

namespace FamilyMoney.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IStateManager _stateManager;

    private int _leftSideWidth = 400;
    private bool _isPaneOpen = false;

    private readonly PeriodViewModel _period;
    private readonly AccountsViewModel _accountsViewModel;
    private readonly TransactionsViewModel _transactionsViewModel;

    public ICommand TriggerPaneCommand { get; }

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

    public MainWindowViewModel(IRepository repository, 
        IStateManager stateManager,
        PeriodViewModel period, 
        AccountsViewModel accounts, 
        TransactionsViewModel transactionsViewModel)
    {
        TriggerPaneCommand = ReactiveCommand.Create(() => IsPaneOpen = !IsPaneOpen);

        _stateManager = stateManager;

        _accountsViewModel = accounts;
        _transactionsViewModel = transactionsViewModel;
        _transactionsViewModel.MainWindowViewModel = this;

        _period = period;

        RxApp.MainThreadScheduler.Schedule(MainInit);
    }

    private void MainInit()
    {
        var state = _stateManager.GetMainState();
        state.PeriodFrom = _period.From;
        state.PeriodTo = _period.To;
        _stateManager.SetMainState(state);
    }
}
