using Avalonia.Controls;
using Avalonia.ReactiveUI;
using FamilyMoney.ViewModels;
using ReactiveUI;
using System.Threading.Tasks;

namespace FamilyMoney.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();

        this.WhenActivated(action =>
            action(ViewModel!.Period.ShowDialog.RegisterHandler(DoShowDialogAsync)));
    }

    private async Task DoShowDialogAsync(InteractionContext<CustomPeriodViewModel, CustomPeriodViewModel?> interaction)
    {
        var dialog = new CustomPeriodWindow();
        dialog.DataContext = interaction.Input;

        var result = await dialog.ShowDialog<CustomPeriodViewModel?>(this);
        interaction.SetOutput(result);
    }
}