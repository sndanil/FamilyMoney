using Avalonia.Controls;
using Avalonia.Interactivity;
using FamilyMoney.Navigation.Controls;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Views;

/// <summary>
/// Период для отчётов: та же пилюля с поповером, что на главном экране, но без хоткеев —
/// иначе они дублировались бы с основным выбором периода.
/// </summary>
public partial class PeriodPickerView : UserControl
{
    public PeriodPickerView()
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
