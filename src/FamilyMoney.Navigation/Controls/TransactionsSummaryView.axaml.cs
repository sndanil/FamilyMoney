using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using FamilyMoney.Navigation.Pages;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Navigation.Controls;

public sealed class CategoryActivatedEventArgs : EventArgs
{
    public CategoryActivatedEventArgs(CategoryTransactionsGroupViewModel group, TransactionsSection section)
    {
        Group = group;
        Section = section;
    }

    public CategoryTransactionsGroupViewModel Group { get; }

    public TransactionsSection Section { get; }
}

public partial class TransactionsSummaryView : UserControl
{
    public TransactionsSummaryView()
    {
        InitializeComponent();
    }

    /// <summary>На широком экране карточка не открывает страницу, а выбирает категорию для правой панели.</summary>
    public event EventHandler<CategoryActivatedEventArgs>? CategoryActivated;

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

        var transactions = main.Transactions;
        var section = transactions.DebetTransactions.Categories.Contains(group)
            ? TransactionsSection.Debet
            : transactions.CreditTransactions.Categories.Contains(group)
                ? TransactionsSection.Credit
                : TransactionsSection.Transfer;

        if (main.IsWideLayout)
        {
            CategoryActivated?.Invoke(this, new CategoryActivatedEventArgs(group, section));
            return;
        }

        var shell = this.FindAncestorOfType<AppShell>();
        if (shell == null)
        {
            return;
        }

        var page = new CategoryPage
        {
            DataContext = new CategoryPageViewModel(main, group, section),
        };

        await shell.NavigationPage.PushAsync(page);
    }
}
