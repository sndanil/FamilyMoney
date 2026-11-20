using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Navigation.Controls;

public partial class TransactionsActionBar : UserControl
{
    public TransactionsActionBar()
    {
        InitializeComponent();
    }

    private async void AddCreditClick(object? sender, RoutedEventArgs e)
    {
        await OpenNewAsync(NewTransactionKind.Credit);
    }

    private async void AddDebetClick(object? sender, RoutedEventArgs e)
    {
        await OpenNewAsync(NewTransactionKind.Debet);
    }

    private async void AddTransferClick(object? sender, RoutedEventArgs e)
    {
        await OpenNewAsync(NewTransactionKind.Transfer);
    }

    private async System.Threading.Tasks.Task OpenNewAsync(NewTransactionKind kind)
    {
        var shell = this.FindAncestorOfType<AppShell>();
        if (shell != null)
        {
            await shell.OpenNewTransactionAsync(kind);
            return;
        }

        // Запасной путь для хоста без AppShell: сохраняем прежнее поведение.
        if (DataContext is MainViewModel main)
        {
            switch (kind)
            {
                case NewTransactionKind.Debet:
                    main.Transactions.AddDebetCommand.Execute(null);
                    break;
                case NewTransactionKind.Credit:
                    main.Transactions.AddCreditCommand.Execute(null);
                    break;
                case NewTransactionKind.Transfer:
                    main.Transactions.AddTransferCommand.Execute(null);
                    break;
            }
        }
    }
}
