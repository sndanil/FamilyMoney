using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    }

    [RelayCommand]
    public async Task CancelAsync()
    {

    }
}
