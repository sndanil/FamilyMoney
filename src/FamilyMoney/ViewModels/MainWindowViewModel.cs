using Avalonia.Media.Imaging;
using DynamicData;
using ReactiveUI;
using System;
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

    public async void LoadAccounts()
    {
        _total = new AccountViewModel(this)
        {
            Name = "Всего",
            Amount = 2000,
        };

        _total.Children.AddRange(new AccountViewModel[]{
            new AccountViewModel(this)
            {
                Name = "Альфа-банк",
                Amount = 1000,
                Image = await LoadImage("D:\\Temp\\Images\\alfabank.png"),
            },
            new AccountViewModel(this)
            {
                Name = "Сбер",
                Amount = 1000,
                Image = await LoadImage("D:\\Temp\\Images\\sber-new.png"),
            },
        });
    }

    private async Task<Avalonia.Media.IImage> LoadImage(string path)
    {
        await using (var stream = System.IO.File.OpenRead(path))
        {
            return await Task.Run(() => Bitmap.DecodeToWidth(stream, 400));
        }
    }
}
