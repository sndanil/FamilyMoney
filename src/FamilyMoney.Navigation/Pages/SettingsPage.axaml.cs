using Avalonia.Controls;
using Avalonia.Interactivity;
using FamilyMoney.Configuration;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Navigation.Pages;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private void OnDepositDaysDecrease(object? sender, RoutedEventArgs e)
    {
        ChangeDepositDays(-1);
    }

    private void OnDepositDaysIncrease(object? sender, RoutedEventArgs e)
    {
        ChangeDepositDays(1);
    }

    private void ChangeDepositDays(int delta)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var days = (int)Math.Round(viewModel.Settings.DepositWarningDays, MidpointRounding.AwayFromZero) + delta;
        viewModel.Settings.DepositWarningDays = Math.Clamp(days, 0, RootConfiguration.MaxDepositWarningDays);
    }
}
