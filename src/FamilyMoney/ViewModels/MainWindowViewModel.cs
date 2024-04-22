using Avalonia.Media.Imaging;
using DynamicData;
using FamilyMoney.DataAccess;
using ReactiveUI;
using Splat;
using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading.Tasks;

namespace FamilyMoney.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private int _leftSideWidth = 400;

    private AccountViewModel? _total = null;
    private AccountViewModel? _selectedAccount = null;

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
        set => this.RaiseAndSetIfChanged(ref _selectedAccount, value);
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
