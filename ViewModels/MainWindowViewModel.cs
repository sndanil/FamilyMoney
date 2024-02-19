﻿using Avalonia.Media.Imaging;
using DynamicData;
using ReactiveUI;
using System.Reactive.Concurrency;
using System.Threading.Tasks;

namespace FamilyMoney.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private int _leftSideWidth = 400;

    private AccountViewModel? _total = null;
    private AccountViewModel? _selectedAccount = null;

    public MainWindowViewModel()
    {
        RxApp.MainThreadScheduler.Schedule(LoadAccounts);
    }

    public int LeftSideWidth 
    { 
        get => _leftSideWidth; 
        set => this.RaiseAndSetIfChanged(ref _leftSideWidth, value); 
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
            },
            new AccountViewModel(this)
            {
                Name = "Сбер",
                Amount = 1000,
            },
        });

        foreach (var total in _total.Children)
        {
            await using (var stream = System.IO.File.OpenRead("D:\\Projects\\logo-sber.png"))
            {
                total.Image = await Task.Run(() => Bitmap.DecodeToWidth(stream, 400));
            }
        }
    }
}
