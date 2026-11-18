using Avalonia.Controls;
using Avalonia.Interactivity;
using FamilyMoney.Navigation.Controls;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Views;

public partial class PeriodView : UserControl
{
    public PeriodView()
    {
        InitializeComponent();
    }

    private void OnPeriodClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PeriodViewModel period)
        {
            return;
        }

        var picker = new PeriodPickerViewModel(period, isExtended: true);
        picker.Applied += (_, _) => PeriodPopup.IsOpen = false;

        PeriodPickerControl.DataContext = picker;
        PeriodPopup.IsOpen = true;
    }
}
