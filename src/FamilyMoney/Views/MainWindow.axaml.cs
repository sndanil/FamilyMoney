using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Messages;
using FamilyMoney.ViewModels;
using FamilyMoney.ViewModels.Settings;

namespace FamilyMoney.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        if (Design.IsDesignMode)
            return;

        WeakReferenceMessenger.Default.Register<MainWindow, ModelEditMessage<AccountViewModel>>(this, static (w, m) =>
        {
            var dialog = new AccountWindow { DataContext = m?.From };
            m?.Reply(dialog.ShowDialog<AccountViewModel?>(w));
        });

        WeakReferenceMessenger.Default.Register<MainWindow, ModelEditMessage<CustomPeriodViewModel>>(this, static (w, m) =>
        {
            var dialog = new CustomPeriodWindow { DataContext = m?.From };
            m?.Reply(dialog.ShowDialog<CustomPeriodViewModel?>(w));
        });

        WeakReferenceMessenger.Default.Register<MainWindow, ModelEditMessage<BaseCategoryViewModel>>(this, static (w, m) =>
        {
            var dialog = new CategoryWindow { DataContext = m?.From };
            m?.Reply(dialog.ShowDialog<BaseCategoryViewModel?>(w));
        });

        WeakReferenceMessenger.Default.Register<MainWindow, ModelEditMessage<BaseTransactionViewModel>>(this, static (w, m) =>
        {
            var dialog = new TransactionWindow { DataContext = m?.From };
            m?.Reply(dialog.ShowDialog<BaseTransactionViewModel?>(w));
        });

        WeakReferenceMessenger.Default.Register<MainWindow, ModelEditMessage<DatabaseViewModel>>(this, static (w, m) =>
        {
            var dialog = new DatabaseWindow { DataContext = m?.From };
            m?.Reply(dialog.ShowDialog<DatabaseViewModel?>(w));
        });

        this.Activated += MainWindowActivated;
    }

    private void MainWindowActivated(object? sender, System.EventArgs e)
    {
        var viewModel = DataContext as MainWindowViewModel;
        if (viewModel is not null && viewModel.CurrentPanel == null)
        {
            viewModel.CurrentPanel = TransactionsPanel;
        }
    }
}