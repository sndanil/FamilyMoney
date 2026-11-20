using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Navigation.Controls;

public partial class CategoryGroupedView : UserControl
{
    public CategoryGroupedView()
    {
        InitializeComponent();
    }

    /// <summary>Пользователь закрыл панель категории: возвращаемся к дням.</summary>
    public event EventHandler? CloseRequested;

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
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

        await shell.OpenTransactionAsync(row);
    }
}
