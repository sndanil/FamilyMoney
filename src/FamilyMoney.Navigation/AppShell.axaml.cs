using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Messages;
using FamilyMoney.Navigation.Pages;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Navigation;

public partial class AppShell : UserControl
{
    private TopLevel? _topLevel;
    private bool? _lastWide;

    public AppShell()
    {
        InitializeComponent();

        SizeChanged += OnShellSizeChanged;

        AttachedToVisualTree += (_, _) =>
        {
            _topLevel = TopLevel.GetTopLevel(this);
            if (_topLevel != null)
            {
                _topLevel.BackRequested += OnBackRequested;
            }
        };

        DetachedFromVisualTree += (_, _) =>
        {
            if (_topLevel != null)
            {
                _topLevel.BackRequested -= OnBackRequested;
                _topLevel = null;
            }
        };

        WeakReferenceMessenger.Default.Register<AppShell, ModelEditMessage<BaseTransactionViewModel>>(this, static (shell, m) =>
        {
            if (m.From == null)
            {
                m.Reply((BaseTransactionViewModel?)null);
                return;
            }

            m.Reply(shell.EditTransactionAsync(m.From));
        });

        // NavigationPage / DrawerPage re-host their child pages, assigning the
        // inherited DataContext before the page bindings are instanced. As a result
        // the bindings never observe the shell view model. Re-applying the
        // DataContext once the page is in the visual tree forces the bindings to
        // re-evaluate against the correct context.
        ShellHome.AttachedToVisualTree += (_, _) => RefreshDataContext(ShellHome);
        ShellMenu.AttachedToVisualTree += (_, _) => RefreshDataContext(ShellMenu);
        DataContextChanged += (_, _) =>
        {
            RefreshDataContext(ShellHome);
            RefreshDataContext(ShellMenu);
        };
    }

    public NavigationPage NavigationPage => ShellNavigation;

    public DrawerPage DrawerPage => ShellDrawer;

    /// <summary>
    /// Строка транзакции, для которой запущено редактирование.
    /// Передаётся странице, чтобы на ней работали копирование и удаление.
    /// </summary>
    public TransactionRowViewModel? PendingTransactionRow { get; set; }

    private void OnShellSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var wide = e.NewSize.Width >= ShellDrawer.DrawerBreakpointLength;
        if (_lastWide == wide)
        {
            return;
        }

        _lastWide = wide;

        // При запуске в портретном режиме ящик должен быть скрыт, в альбомном —
        // открыт, если позволяет ширина.
        ShellDrawer.IsOpen = wide;
    }

    private void OnBackRequested(object? sender, RoutedEventArgs e)
    {
        // Системный «назад» / свайп от края: сначала закрываем страницу,
        // затем — открытый ящик. На корневой странице отдаём событие системе.
        if (ShellNavigation.CanGoBack)
        {
            e.Handled = true;
            _ = ShellNavigation.PopAsync();
            return;
        }

        if (ShellDrawer.IsOpen
            && ShellDrawer.DisplayMode is SplitViewDisplayMode.Overlay or SplitViewDisplayMode.CompactOverlay)
        {
            e.Handled = true;
            ShellDrawer.IsOpen = false;
        }
    }

    private async System.Threading.Tasks.Task<BaseTransactionViewModel?> EditTransactionAsync(BaseTransactionViewModel viewModel)
    {
        var row = PendingTransactionRow;
        PendingTransactionRow = null;

        var page = new TransactionEditPage(viewModel, row);
        await ShellNavigation.PushAsync(page);
        return await page.Result;
    }

    private void RefreshDataContext(Control page)
    {
        Dispatcher.UIThread.Post(() =>
        {
            page.DataContext = null;
            page.DataContext = DataContext;
        }, DispatcherPriority.Loaded);
    }
}
