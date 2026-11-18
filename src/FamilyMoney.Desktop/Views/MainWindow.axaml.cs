using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Messages;
using FamilyMoney.ViewModels;
using FamilyMoney.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyMoney.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        if (Design.IsDesignMode)
        {
            return;
        }

        Opened += OnOpened;
        Closing += OnClosing;

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

        WeakReferenceMessenger.Default.Register<MainWindow, ModelEditMessage<MergeSubCategoriesViewModel>>(this, static (w, m) =>
        {
            var dialog = new MergeSubCategoriesWindow { DataContext = m?.From };
            m?.Reply(dialog.ShowDialog<MergeSubCategoriesViewModel?>(w));
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
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var (width, height, maximized) = viewModel.GetWindowPlacement();
        ApplySavedSize(width, height);

        if (maximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        // Ширину «нормального» окна не затираем размерами развёрнутого:
        // иначе после снятия Maximized окно окажется во весь экран.
        var maximized = WindowState == WindowState.Maximized;
        viewModel.SaveLayout(
            maximized ? null : (int)Width,
            maximized ? null : (int)Height,
            maximized);
    }

    /// <summary>Возвращает окно в экран, если сохранённый размер больше рабочей области.</summary>
    private void ApplySavedSize(int width, int height)
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        var scaling = screen?.Scaling ?? 1d;
        var maxWidth = screen is null ? width : screen.WorkingArea.Width / scaling;
        var maxHeight = screen is null ? height : screen.WorkingArea.Height / scaling;

        Width = Math.Clamp(width, 800d, Math.Max(800d, maxWidth));
        Height = Math.Clamp(height, 500d, Math.Max(500d, maxHeight));
    }

    private void AccountsSplitter_DragCompleted(object? sender, VectorEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SaveLayout();
        }
    }

    private void OnSyncClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && viewModel.Settings.IsSyncAvailable)
        {
            (this.FindResource("SyncMenu") as MenuFlyout)?.ShowAt(SyncButton);
            return;
        }

        OnPeerSyncClick(sender, e);
    }

    private void OnSyncCloudClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.Settings.SyncNowCommand.Execute(null);
        }
    }

    private async void OnPeerSyncClick(object? sender, RoutedEventArgs e)
    {
        var host = AppInit.GlobalHost?.Services.GetService<PeerHostViewModel>();
        if (host == null)
        {
            return;
        }

        var window = new PeerSyncWindow { DataContext = host };
        await window.ShowDialog(this);
    }
}
