using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Messages;
using FamilyMoney.Utils;
using FamilyMoney.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;


namespace FamilyMoney.Views;

public partial class TransactionWindow :Window
{
    private bool _skipCategoryChange = false;

    public BaseTransactionViewModel? ViewModel { get => DataContext as BaseTransactionViewModel; }

    public TransactionWindow()
    {
        InitializeComponent();

        WeakReferenceMessenger.Default.Register<TransactionWindow, ModelCloseMessage<BaseTransactionViewModel>>(this, static (w, m) => w.Close(m.Result));
        WeakReferenceMessenger.Default.Register<TransactionWindow, SetFocusOnMessage>(this, (w, m) =>
        {
            switch (m.PropertyName)
            {
                case nameof(ViewModel.Sum):
                    Dispatcher.UIThread.Post(() => SumPicker.Focus());
                    break;
                default:
                    break;
            }
        });

        this.Activated += (s, e) =>
        {
            var viewModel = DataContext as BaseTransactionViewModel;
            if (viewModel == null)
            {
                return;
            }

            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(viewModel.Category) && !_skipCategoryChange)
                {
                    viewModel!.SubCategoryText = null;
                    viewModel!.SubCategory = null;
                    SubCategoryCompleteBox.Focus();
                }

                if (e.PropertyName == nameof(viewModel.SubCategory))
                {
                    if (viewModel!.SubCategory != null)
                    {
                        viewModel.Comments = viewModel!.SubCategory.Comments;
                    }
                    else
                    {
                        viewModel.Comments = [];
                    }
                }
            };
        };

        this.Activated += TransactionWindowActivated;

        SubCategoryCompleteBox.ItemSelector = ItemSelector;
        SubCategoryCompleteBox.ItemFilter = ItemFilter;

        var sumFocused = false;
        SubCategoryCompleteBox.DropDownClosed += (s, e) =>
        {
            var viewModel = DataContext as BaseTransactionViewModel;
            if (viewModel == null)
            {
                return;
            }

            if (viewModel!.SubCategory != null)
            {
                if (viewModel!.Sum == 0)
                {
                    viewModel!.Sum = viewModel!.SubCategory.LastSum;
                }

                if (viewModel!.Category == null)
                {
                    _skipCategoryChange = true;
                    viewModel!.Category = viewModel!.Categories?.FirstOrDefault(c => c.Id == viewModel!.SubCategory.CategoryId);
                    _skipCategoryChange = false;
                }


                Task.Run(async () =>
                {
                    if (!sumFocused)
                    {
                        sumFocused = true;
                        WeakReferenceMessenger.Default.Send(new SetFocusOnMessage(nameof(viewModel.Sum)));
                    }

                    await Task.Delay(1000);
                    sumFocused = false;
                });
            }
        };
    }

    private void TransactionWindowActivated(object? sender, EventArgs e)
    {
        if (ViewModel?.Account == null)
        {
            AccountComboBox.Focus();
        }
        else if (ViewModel?.Category == null || ViewModel?.SubCategory == null)
        {
            SubCategoryCompleteBox.Focus();
        }
        else
        {
            SumPicker.Focus();
        }
    }

    private string ItemSelector(string? search, object item)
    {
        if (item is BaseSubCategoryViewModel viewModel)
        {
            return viewModel.Name;
        }

        return search ?? string.Empty;
    }

    private bool ItemFilter(string? search, object? item)
    {
        var transaction = ViewModel;

        if (item is BaseSubCategoryViewModel subCategory)
        {
            if (!string.IsNullOrEmpty(search))
            {
                return (subCategory.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) == true
                    || subCategory.Name?.Contains(KeyboardHelper.Translate(search), StringComparison.OrdinalIgnoreCase) == true
                    || subCategory.Category?.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) == true
                    || subCategory.Category?.Name?.Contains(KeyboardHelper.Translate(search), StringComparison.OrdinalIgnoreCase) == true)
                    && (transaction?.Category == null || subCategory.CategoryId == transaction?.Category.Id);
            }

            return transaction?.Category == null || transaction?.Category.Id == subCategory.CategoryId;
        }

        return true;
    }

    private void ShowDropDownButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SubCategoryCompleteBox.ShowDropDown();
    }

    private void ClearSubCategoryButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SubCategoryCompleteBox.SelectedItem = null;
        SubCategoryCompleteBox.Text = string.Empty;
    }

    private void CommentClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        this.ViewModel!.Comment += (sender as MenuItem)?.Header;
    }

    private void TagInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ViewModel?.AddTagCommand.CanExecute(null) == true)
        {
            ViewModel.AddTagCommand.Execute(null);
            e.Handled = true;
        }
    }

}
