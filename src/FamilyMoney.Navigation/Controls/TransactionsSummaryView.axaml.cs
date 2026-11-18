using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using FamilyMoney.Navigation.Pages;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Navigation.Controls;

public partial class TransactionsSummaryView : UserControl
{
    public TransactionsSummaryView()
    {
        InitializeComponent();
    }

    private async void OnCategoryTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainViewModel main)
        {
            return;
        }

        if ((sender as Control)?.DataContext is not CategoryTransactionsGroupViewModel group)
        {
            return;
        }

        var shell = this.FindAncestorOfType<AppShell>();
        if (shell == null)
        {
            return;
        }

        var transactions = main.Transactions;
        var section = transactions.DebetTransactions.Categories.Contains(group)
            ? TransactionsSection.Debet
            : transactions.CreditTransactions.Categories.Contains(group)
                ? TransactionsSection.Credit
                : TransactionsSection.Transfer;

        var page = new CategoryPage
        {
            DataContext = new CategoryPageViewModel(main, group, section),
        };

        await shell.NavigationPage.PushAsync(page);
    }
}
