using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Messages;
using System;
using System.Threading.Tasks;

namespace FamilyMoney.ViewModels;

public partial class CustomPeriodViewModel: ViewModelBase
{
    [ObservableProperty]
    public partial DateTime From { get; set;  }

    [ObservableProperty]
    public partial DateTime To { get; set; }

    [RelayCommand]
    public async Task OkAsync()
    {
        WeakReferenceMessenger.Default.Send(new ModelCloseMessage<CustomPeriodViewModel>(this));
    }

    [RelayCommand]
    public async Task CancelAsync()
    {
        WeakReferenceMessenger.Default.Send(new ModelCloseMessage<CustomPeriodViewModel>(this));
    }
}
