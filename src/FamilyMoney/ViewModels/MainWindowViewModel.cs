using FamilyMoney.DataAccess;
using ReactiveUI;
using Splat;
using System;
using System.Reactive.Concurrency;

namespace FamilyMoney.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private int _leftSideWidth = 400;

    private AccountViewModel? _total = null;
    private AccountViewModel? _selectedAccount = null;
    private AccountViewModel? _draggingAccount = null;

    private PeriodViewModel _period;

    public MainWindowViewModel()
    {
        _period = new PeriodViewModel 
        { 
            From = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
            To = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1).AddDays(-1),
            PeriodType = PeriodType.Month 
        };
        _period.PropertyChanged += (e, a) => this.RaisePropertyChanged(nameof(Period));

        RxApp.MainThreadScheduler.Schedule(LoadAccounts);
    }

    public int LeftSideWidth 
    { 
        get => _leftSideWidth; 
        set => this.RaiseAndSetIfChanged(ref _leftSideWidth, value); 
    }

    public PeriodViewModel Period
    {
        get => _period;
        set => this.RaiseAndSetIfChanged(ref _period, value);
    }

    public AccountViewModel? Total
    {
        get => _total;
        set => this.RaiseAndSetIfChanged(ref _total, value);
    }

    public AccountViewModel? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            Total!.IsSelected = false;
            foreach (var group in Total.Children)
            {
                group.IsSelected = false;
                foreach (var account in group.Children)
                {
                    account.IsSelected = false;
                }
            }

            value!.IsSelected = true;
            this.RaiseAndSetIfChanged(ref _selectedAccount, value);
        }
    }

    public AccountViewModel? DraggingAccount
    {
        get => _draggingAccount;
        set => this.RaiseAndSetIfChanged(ref _draggingAccount, value);
    }

    private void LoadAccounts()
    {
        _total = new AccountViewModel(this)
        {
            Name = "Всего",
            Amount = 2000,
        };

        var repository = Locator.Current.GetService<IRepository>();
        var accounts = repository!.GetAccounts();
        _total.AddFromAccount(repository, accounts);
    }
}
