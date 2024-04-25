using FamilyMoney.DataAccess;
using ReactiveUI;
using Splat;
using System;
using System.Reactive.Concurrency;
using System.Windows.Input;

namespace FamilyMoney.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private int _leftSideWidth = 400;
    private bool _isPaneOpen = false;

    private AccountsViewModel _accountsViewModel;

    public ICommand TriggerPaneCommand { get; }

    private PeriodViewModel _period;

    public MainWindowViewModel(IRepository repository, AccountsViewModel accounts)
    {
        TriggerPaneCommand = ReactiveCommand.Create(() => IsPaneOpen = !IsPaneOpen);

        _accountsViewModel = accounts;

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
        set => this.RaiseAndSetIfChanged(ref _period, value);
    }

    public AccountsViewModel Accounts
    {
        get => _accountsViewModel;
        set => this.RaiseAndSetIfChanged(ref _accountsViewModel, value);
    }

}
