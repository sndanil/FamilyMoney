using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Navigation.Pages;

public partial class CategoryPage : ContentPage
{
    public CategoryPage()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        (DataContext as CategoryPageViewModel)?.Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        (DataContext as CategoryPageViewModel)?.Detach();
        base.OnDetachedFromVisualTree(e);
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
