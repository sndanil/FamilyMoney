using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Navigation.Pages;

public partial class SelectionPage : ContentPage
{
    private readonly TaskCompletionSource<object?> _result = new();
    private bool _ready;
    private bool _closing;

    public SelectionPage()
    {
        InitializeComponent();
    }

    public SelectionPage(SelectionViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
        Header = viewModel.Title;

        AttachedToVisualTree += (_, _) =>
            Dispatcher.UIThread.Post(() => _ready = true, DispatcherPriority.Loaded);
        DetachedFromVisualTree += (_, _) =>
        {
            if (!_closing)
            {
                _result.TrySetResult(null);
            }

            _closing = true;
        };
    }

    public Task<object?> Result => _result.Task;

    private async void SelectionList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _closing || e.AddedItems.Count == 0)
        {
            return;
        }

        var item = e.AddedItems[0];
        if (item is AccountViewModel { IsGroup: true })
        {
            SelectionList.SelectedItem = null;
            return;
        }

        _closing = true;
        _result.TrySetResult(item);
        if (Navigation is { } navigation)
        {
            await navigation.PopAsync();
        }
    }

    private void ClearSearchClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is SelectionViewModel viewModel)
        {
            viewModel.SearchText = string.Empty;
        }
    }
}
