using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FamilyMoney.Navigation.Controls;

public partial class PeriodPicker : UserControl
{
    public PeriodPicker()
    {
        InitializeComponent();
    }

    private void OnCellClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: PeriodPickerCell cell } && DataContext is PeriodPickerViewModel viewModel)
        {
            viewModel.SelectCell(cell);
        }
    }
}
