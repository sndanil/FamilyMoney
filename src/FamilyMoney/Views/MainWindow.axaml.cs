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

        this.WhenActivated(action => action(ViewModel!.Period.ShowDialog.RegisterHandler(DoShowCustomPeriodDialogAsync)));
        this.WhenActivated(action => action(AccountViewModel.ShowDialog.RegisterHandler(DoShowAccountEditDialogAsync)));
        this.WhenActivated(action => action(TransactionViewModel.ShowDialog.RegisterHandler(DoShowTransactionEditDialogAsync)));
    }

    private async Task DoShowCustomPeriodDialogAsync(InteractionContext<CustomPeriodViewModel, CustomPeriodViewModel?> interaction)
    {
        var dialog = new CustomPeriodWindow();
        dialog.DataContext = interaction.Input;

        var result = await dialog.ShowDialog<CustomPeriodViewModel?>(this);
        interaction.SetOutput(result);
    }

    private async Task DoShowAccountEditDialogAsync(InteractionContext<AccountViewModel, AccountViewModel?> interaction)
    {
        var dialog = new AccountWindow();
        dialog.DataContext = interaction.Input;

        var result = await dialog.ShowDialog<AccountViewModel?>(this);
        interaction.SetOutput(result);
    }

    private async Task DoShowTransactionEditDialogAsync(InteractionContext<TransactionViewModel, TransactionViewModel?> interaction)
    {
        var dialog = new TransactionWindow();
        dialog.DataContext = interaction.Input;

        var result = await dialog.ShowDialog<TransactionViewModel?>(this);
        interaction.SetOutput(result);
    }
}