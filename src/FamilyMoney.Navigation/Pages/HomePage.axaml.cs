using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using FamilyMoney.Navigation.Controls;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Navigation.Pages;

public partial class HomePage : ContentPage
{
    public HomePage()
    {
        InitializeComponent();
    }

    private void OnPeriodClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var picker = new PeriodPickerViewModel(viewModel.Period);
        picker.Applied += (_, _) => PeriodPopup.IsOpen = false;

        PeriodPickerControl.DataContext = picker;
        PeriodPopup.IsOpen = true;
    }

    private async void OnTransactionTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not TransactionRowViewModel row || row.Parent == null)
        {
            return;
        }

        var shell = this.FindAncestorOfType<AppShell>();
        if (shell == null)
        {
            return;
        }

        shell.PendingTransactionRow = row;
        await row.Parent.EditCommand.ExecuteAsync(row);
    }
}
