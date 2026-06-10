using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Messages;
using FamilyMoney.Navigation.Utils;
using FamilyMoney.Utils;
using FamilyMoney.ViewModels;
using System.ComponentModel;

namespace FamilyMoney.Navigation.Pages;

public partial class TransactionEditPage : ContentPage
{
    private readonly BaseTransactionViewModel _viewModel = null!;
    private readonly TransactionRowViewModel? _row;
    private readonly TaskCompletionSource<BaseTransactionViewModel?> _result = new();
    private bool _skipCategoryChange;
    private bool _closing;

    public TransactionEditPage()
    {
        InitializeComponent();
    }

    public TransactionEditPage(BaseTransactionViewModel viewModel, TransactionRowViewModel? row)
        : this()
    {
        _viewModel = viewModel;
        _row = row;
        DataContext = viewModel;

        Header = viewModel switch
        {
            TransferTransactionViewModel => "Перевод",
            DebetTransactionViewModel => "Доход",
            CreditTransactionViewModel => "Расход",
            _ => "Транзакция",
        };

        // Копирование и удаление доступны только для существующей транзакции.
        var hasRow = row != null;
        DeleteButton.IsVisible = hasRow;
        CopyButton.IsVisible = hasRow;

        SubCategoryCompleteBox.ItemSelector = SubCategoryItemSelector;
        SubCategoryCompleteBox.ItemFilter = SubCategoryItemFilter;
        SubCategoryCompleteBox.DropDownClosed += SubCategoryDropDownClosed;

        _viewModel.PropertyChanged += ViewModelPropertyChanged;
        WeakReferenceMessenger.Default.Register<TransactionEditPage, ModelCloseMessage<BaseTransactionViewModel>>(
            this,
            static (page, m) => page.Close(m.Result));

        DetachedFromVisualTree += (_, _) =>
        {
            // Срабатывает и при возврате системной кнопкой "назад" — закрытие без сохранения.
            _closing = true;
            Cleanup();
            _result.TrySetResult(null);
        };
    }

    /// <summary>Результат редактирования: ViewModel при сохранении, null при отмене или удалении.</summary>
    public Task<BaseTransactionViewModel?> Result => _result.Task;

    private void Close(BaseTransactionViewModel? result)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        Cleanup();
        _result.TrySetResult(result);
        Navigation?.PopAsync();
    }

    private void Cleanup()
    {
        _viewModel.PropertyChanged -= ViewModelPropertyChanged;
        WeakReferenceMessenger.Default.Unregister<ModelCloseMessage<BaseTransactionViewModel>>(this);
    }

    private async void DeleteConfirmClick(object? sender, RoutedEventArgs e)
    {
        var row = _row;
        Close(null);
        if (row?.Parent != null)
        {
            await row.Parent.DeleteCommand.ExecuteAsync(row);
        }
    }

    private async void CopyClick(object? sender, RoutedEventArgs e)
    {
        var row = _row;
        Close(null);
        if (row?.Parent != null)
        {
            // CopyCommand откроет новую страницу редактирования с предзаполненной копией.
            await row.Parent.CopyCommand.ExecuteAsync(row);
        }
    }

    private void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_viewModel.Category) && !_skipCategoryChange)
        {
            _viewModel.SubCategoryText = null;
            _viewModel.SubCategory = null;
        }

        if (e.PropertyName is nameof(_viewModel.SubCategory) or nameof(_viewModel.Category))
        {
            _viewModel.RefreshSuggestedTags();
        }

        if (e.PropertyName == nameof(_viewModel.SubCategory))
        {
            _viewModel.Comments = _viewModel.SubCategory?.Comments ?? [];
        }
    }

    private void SubCategoryDropDownClosed(object? sender, EventArgs e)
    {
        if (_viewModel.SubCategory == null)
        {
            return;
        }

        if (_viewModel.Sum == 0)
        {
            _viewModel.Sum = _viewModel.SubCategory.LastSum;
        }

        if (_viewModel.Category == null)
        {
            _skipCategoryChange = true;
            _viewModel.Category = _viewModel.Categories?.FirstOrDefault(c => c.Id == _viewModel.SubCategory.CategoryId);
            _skipCategoryChange = false;
        }
    }

    private string SubCategoryItemSelector(string? search, object item)
    {
        if (item is BaseSubCategoryViewModel viewModel)
        {
            return viewModel.Name ?? string.Empty;
        }

        return search ?? string.Empty;
    }

    private bool SubCategoryItemFilter(string? search, object? item)
    {
        if (item is not BaseSubCategoryViewModel subCategory)
        {
            return true;
        }

        if (!string.IsNullOrEmpty(search))
        {
            return (subCategory.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) == true
                || subCategory.Name?.Contains(KeyboardHelper.Translate(search), StringComparison.OrdinalIgnoreCase) == true
                || subCategory.Category?.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) == true
                || subCategory.Category?.Name?.Contains(KeyboardHelper.Translate(search), StringComparison.OrdinalIgnoreCase) == true)
                && (_viewModel.Category == null || subCategory.CategoryId == _viewModel.Category.Id);
        }

        return _viewModel.Category == null || _viewModel.Category.Id == subCategory.CategoryId;
    }

    private void ShowSubCategoryDropDownClick(object? sender, RoutedEventArgs e)
    {
        SubCategoryCompleteBox.ShowDropDown();
    }

    private void ClearSubCategoryClick(object? sender, RoutedEventArgs e)
    {
        SubCategoryCompleteBox.SelectedItem = null;
        SubCategoryCompleteBox.Text = string.Empty;
    }

    private void CommentClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.Comment += (sender as MenuItem)?.Header;
    }
}
