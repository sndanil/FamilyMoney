using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Messages;
using FamilyMoney.Navigation.Pages;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Navigation;

public partial class AppShell : UserControl
{
    private TopLevel? _topLevel;
    private bool? _lastWide;
    private bool _warmUpStarted;
    private readonly HashSet<Control> _contextRefreshed = [];

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

            StartWarmUp();
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
            _contextRefreshed.Clear();
            RefreshDataContext(ShellHome);
            RefreshDataContext(ShellMenu);
            StartWarmUp();
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

    /// <summary>
    /// Мгновенно открывает форму транзакции с пустой заблокированной формой,
    /// затем подгружает данные и заменяет заглушку реальной транзакцией.
    /// </summary>
    public async Task OpenTransactionAsync(TransactionRowViewModel row)
    {
        if (row.Parent == null)
        {
            return;
        }

        var page = new TransactionEditPage(row);
        await ShellNavigation.PushAsync(page);

        if (page.IsClosed)
        {
            return;
        }

        try
        {
            var (transaction, viewModel) = await row.Parent.PrepareEditAsync(row);
            if (viewModel == null || page.IsClosed)
            {
                return;
            }

            page.Load(viewModel);
            var result = await page.Result;
            if (result != null && transaction != null)
            {
                await row.Parent.SavePreparedAsync(transaction, result);
            }
        }
        catch
        {
            if (ReferenceEquals(ShellNavigation.CurrentPage, page))
            {
                await ShellNavigation.PopAsync();
            }
        }
    }

    /// <summary>
    /// То же мгновенное открытие, но для новой транзакции из панели действий.
    /// </summary>
    public async Task OpenNewTransactionAsync(NewTransactionKind kind)
    {
        if (DataContext is not MainViewModel main)
        {
            return;
        }

        var page = new TransactionEditPage(null, loading: true);
        await ShellNavigation.PushAsync(page);

        if (page.IsClosed)
        {
            return;
        }

        try
        {
            var (transaction, viewModel) = await main.Transactions.PrepareNewAsync(kind);
            if (viewModel == null || page.IsClosed)
            {
                return;
            }

            page.Load(viewModel);
            var result = await page.Result;
            if (result != null && transaction != null)
            {
                await main.Transactions.SavePreparedAsync(transaction, result);
            }
        }
        catch
        {
            if (ReferenceEquals(ShellNavigation.CurrentPage, page))
            {
                await ShellNavigation.PopAsync();
            }
        }
    }

    private async Task<BaseTransactionViewModel?> EditTransactionAsync(BaseTransactionViewModel viewModel)
    {
        var row = PendingTransactionRow;
        PendingTransactionRow = null;

        var page = new TransactionEditPage(viewModel, row);
        await ShellNavigation.PushAsync(page);
        return await page.Result;
    }

    private void StartWarmUp()
    {
        if (_warmUpStarted || DataContext is not MainViewModel)
        {
            return;
        }

        _warmUpStarted = true;

        // Не мешаем первому кадру главного экрана: прогреваемся после короткой паузы.
        Dispatcher.UIThread.Post(async () =>
        {
            await Task.Delay(300);

            if (DataContext is not MainViewModel main)
            {
                return;
            }

            // Тяжёлые списки категорий/подкатегорий строятся один раз в фоне,
            // чтобы первый тап не ждал несколько проходов по базе.
            _ = main.Transactions.WarmUpAsync();

            // Прогреваем и саму страницу (XAML/шаблоны), чтобы при первом тапе
            // она открывалась без задержки на инициализацию контролов.
            _ = new TransactionEditPage();
        }, DispatcherPriority.Background);
    }

    private void RefreshDataContext(Control page)
    {
        if (_contextRefreshed.Contains(page))
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (_contextRefreshed.Contains(page) || TopLevel.GetTopLevel(page) == null)
            {
                return;
            }

            page.DataContext = null;
            page.DataContext = DataContext;
            _contextRefreshed.Add(page);
        }, DispatcherPriority.Loaded);
    }
}
