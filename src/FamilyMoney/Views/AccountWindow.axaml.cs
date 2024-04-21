using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using FamilyMoney.ViewModels;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;

namespace FamilyMoney.Views;

public partial class AccountWindow : ReactiveWindow<AccountViewModel>
{
    public AccountWindow()
    {
        InitializeComponent();

        this.WhenActivated(action => action(ViewModel!.OkCommand.Subscribe(Close)));
        this.WhenActivated(action => action(ViewModel!.CancelCommand.Subscribe(Close)));
    }
}