using ReactiveUI.Avalonia;
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
        this.WhenActivated(action => action(BaseCategoryViewModel.ShowDialog.RegisterHandler(DoShowCategoryEditDialogAsync)));
        this.WhenActivated(action => action(BaseTransactionViewModel.ShowDialog.RegisterHandler(DoShowTransactionEditDialogAsync)));

        this.Activated += MainWindowActivated;
    }

    private void MainWindowActivated(object? sender, System.EventArgs e)
    {
        if (ViewModel is not null && ViewModel.CurrentPanel == null)
        {
            ViewModel.CurrentPanel = TransactionsPanel;
        }
    }

    private async Task DoShowCustomPeriodDialogAsync(IInteractionContext<CustomPeriodViewModel, CustomPeriodViewModel?> interaction)
    {
        var dialog = new CustomPeriodWindow();
        dialog.DataContext = interaction.Input;

        var result = await dialog.ShowDialog<CustomPeriodViewModel?>(this);
        interaction.SetOutput(result);
    }

    private async Task DoShowAccountEditDialogAsync(IInteractionContext<AccountViewModel, AccountViewModel?> interaction)
    {
        var dialog = new AccountWindow();
        dialog.DataContext = interaction.Input;

        var result = await dialog.ShowDialog<AccountViewModel?>(this);
        interaction.SetOutput(result);
    }

    private async Task DoShowCategoryEditDialogAsync(IInteractionContext<BaseCategoryViewModel, BaseCategoryViewModel?> interaction)
    {
        var dialog = new CategoryWindow();
        dialog.DataContext = interaction.Input;

        var result = await dialog.ShowDialog<BaseCategoryViewModel?>(this);
        interaction.SetOutput(result);
    }

    private async Task DoShowTransactionEditDialogAsync(IInteractionContext<BaseTransactionViewModel, BaseTransactionViewModel?> interaction)
    {
        var dialog = new TransactionWindow();
        dialog.DataContext = interaction.Input;

        var result = await dialog.ShowDialog<BaseTransactionViewModel?>(this);
        interaction.SetOutput(result);
    }
}