using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FamilyMoney.Navigation.Controls;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Navigation.Pages;

public partial class HomePage : ContentPage
{
    // На такой ширине страницы сводка и транзакции помещаются рядом.
    private const double WideLayoutThreshold = 700;

    private CategoryPageViewModel? _selectedCategory;
    private TransactionsSection _selectedSection;

    public HomePage()
    {
        InitializeComponent();

        SizeChanged += (_, e) => ApplyWidth(e.NewSize.Width);
        DataContextChanged += (_, _) => ApplyWidth(Bounds.Width);
        // Ширина окончательно становится известна после раскладки, когда DataContext
        // уже установлен; SizeChanged к этому моменту мог пройти раньше.
        LayoutUpdated += (_, _) => ApplyWidth(Bounds.Width);

        CompactSummary.CategoryActivated += OnCategoryActivated;
        WideSummary.CategoryActivated += OnCategoryActivated;
        WideCategory.CloseRequested += (_, _) => ClearSelectedCategory();
    }

    private void ApplyWidth(double width)
    {
        if (DataContext is not MainViewModel main || width <= 0)
        {
            return;
        }

        var wide = width >= WideLayoutThreshold;
        if (main.IsWideLayout == wide)
        {
            return;
        }

        main.IsWideLayout = wide;

        if (!wide)
        {
            // В узкой раскладке панели категории нет: не оставляем висящий контекст.
            ClearSelectedCategory();
        }
    }

    private void OnCategoryActivated(object? sender, CategoryActivatedEventArgs e)
    {
        if (DataContext is not MainViewModel main)
        {
            return;
        }

        // Повторный выбор той же карточки закрывает панель.
        if (_selectedCategory is { } current
            && current.Category?.Category?.Id == e.Group.Category?.Id
            && _selectedSection == e.Section)
        {
            ClearSelectedCategory();
            return;
        }

        ClearSelectedCategory();

        var viewModel = new CategoryPageViewModel(main, e.Group, e.Section);
        viewModel.Attach();
        _selectedCategory = viewModel;
        _selectedSection = e.Section;

        WideCategory.DataContext = viewModel;
        WideCategory.IsVisible = true;
        WideDays.IsVisible = false;
    }

    private void ClearSelectedCategory()
    {
        if (_selectedCategory is { } viewModel)
        {
            viewModel.Detach();
            _selectedCategory = null;
        }

        WideCategory.DataContext = null;
        WideCategory.IsVisible = false;
        WideDays.IsVisible = true;
    }

    private void OnPeriodClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var picker = new PeriodPickerViewModel(viewModel.Period);
        picker.Applied += (_, _) => PeriodPopup.IsOpen = false;

        PeriodPickerControl.DataContext = picker;
        PeriodPopup.IsOpen = true;
    }

    private void OnSyncClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        // Оба способа — показываем выбор; один — сразу выполняем его.
        if (viewModel.Settings.IsSyncAvailable && viewModel.Settings.PeerSync.IsAvailable)
        {
            (this.FindResource("SyncMenu") as MenuFlyout)?.ShowAt(SyncButton);
            return;
        }

        if (viewModel.Settings.IsSyncAvailable)
        {
            viewModel.Settings.SyncNowCommand.Execute(null);
            return;
        }

        _ = OpenPeerSyncAsync();
    }

    private void OnSyncCloudClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.Settings.SyncNowCommand.Execute(null);
        }
    }

    private void OnPeerSyncClick(object? sender, RoutedEventArgs e) => _ = OpenPeerSyncAsync();

    private void OnSyncSettingsClick(object? sender, RoutedEventArgs e) => _ = OpenSettingsAsync();

    private async Task OpenPeerSyncAsync()
    {
        var shell = this.FindAncestorOfType<AppShell>();
        if (shell == null || shell.NavigationPage.CurrentPage is PeerSyncPage)
        {
            return;
        }

        await shell.NavigationPage.PushAsync(new PeerSyncPage { DataContext = DataContext });
    }

    private async Task OpenSettingsAsync()
    {
        var shell = this.FindAncestorOfType<AppShell>();
        if (shell == null || shell.NavigationPage.CurrentPage is SettingsPage)
        {
            return;
        }

        await shell.NavigationPage.PushAsync(new SettingsPage { DataContext = DataContext });
    }
}
