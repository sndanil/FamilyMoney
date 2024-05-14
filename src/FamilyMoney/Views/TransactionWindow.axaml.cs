using Avalonia.Controls;
using Avalonia.ReactiveUI;
using FamilyMoney.Utils;
using FamilyMoney.ViewModels;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;


namespace FamilyMoney.Views;

public partial class TransactionWindow : ReactiveWindow<BaseTransactionViewModel>
{
    public TransactionWindow()
    {
        InitializeComponent();

        this.WhenActivated(action => action(ViewModel!.OkCommand.Subscribe(Close)));
        this.WhenActivated(action => action(ViewModel!.CancelCommand.Subscribe(Close)));

        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(v => v.ViewModel!.Sum)
                .Skip(1)
                .Do(v =>
                {
                    this.ViewModel!.ToSum = this.ViewModel!.Sum;
                })
                .Subscribe();

            this.WhenAnyValue(v => v.ViewModel!.Category)
                .Skip(1)
                .Do(v =>
                {
                    this.ViewModel!.SubCategoryText = null;
                    this.ViewModel!.SubCategory = null;
                    RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(50), () => SubCategoryCompleteBox.Focus());
                })
                .Subscribe();

            this.WhenAnyValue(v => v.ViewModel!.SubCategory)
                .Skip(1)
                .Do(v =>
                {
                    if (this.ViewModel!.Sum == 0 && this.ViewModel!.SubCategory != null)
                    {
                        this.ViewModel!.Sum = this.ViewModel!.SubCategory.LastSum;
                    }

                    RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(50), () => SumPicker.Focus());
                })
                .Subscribe();

            this.WhenAnyValue(v => v.ViewModel!.SubCategory)
                .Do(v =>
                {
                    if (this.ViewModel!.SubCategory != null)
                    {
                        this.ViewModel.Comments = this.ViewModel!.SubCategory.Comments;
                    }
                    else
                    {
                        this.ViewModel.Comments = [];
                    }
                })
                .Subscribe();
        });

        this.Activated += TransactionWindowActivated;

        SubCategoryCompleteBox.ItemSelector = ItemSelector;
        SubCategoryCompleteBox.ItemFilter = ItemFilter;
    }

    private void TransactionWindowActivated(object? sender, EventArgs e)
    {
        if (ViewModel?.Account == null)
        {
            AccountComboBox.Focus();
        }
        else if (ViewModel?.Category == null)
        {
            CategoryComboBox.Focus();
        }
        else if (ViewModel?.SubCategory == null)
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
        var subCategory = item as BaseSubCategoryViewModel;

        if (subCategory != null)
        {
            if (!string.IsNullOrEmpty(search))
            {
                return (subCategory.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) == true
                    || subCategory.Name?.Contains(KeyboardHelper.Translate(search), StringComparison.OrdinalIgnoreCase) == true)
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
    }

    private void CommentClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        this.ViewModel!.Comment += (sender as MenuItem)?.Header;
    }

}
