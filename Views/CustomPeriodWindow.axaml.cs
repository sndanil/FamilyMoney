using System;
using Avalonia.ReactiveUI;
using FamilyMoney.ViewModels;
using ReactiveUI;


namespace FamilyMoney.Views;

public partial class CustomPeriodWindow : ReactiveWindow<CustomPeriodViewModel>
{
    public CustomPeriodWindow()
    {
        InitializeComponent();

        this.WhenActivated(action => action(ViewModel!.OkCommand.Subscribe(Close)));
        this.WhenActivated(action => action(ViewModel!.CancelCommand.Subscribe(Close)));
    }
}