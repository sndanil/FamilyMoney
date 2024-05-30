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
    private bool _skipCategoryChange = false;

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
                .Where(v => !_skipCategoryChange)
                .Do(v =>
                {
                    this.ViewModel!.SubCategoryText = null;
                    this.ViewModel!.SubCategory = null;
                    RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(50), () => SubCategoryCompleteBox.Focus());
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

        bool sumFocused = false;
        SubCategoryCompleteBox.DropDownClosed += (s, e) =>
        {
            if (!sumFocused)
            {
                sumFocused = true;
                RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(50), () => 
                {
                    if (this.ViewModel!.SubCategory != null)
                    {
                        if (this.ViewModel!.Sum == 0)
                        {
                            this.ViewModel!.Sum = this.ViewModel!.SubCategory.LastSum;
                        }

                        if (this.ViewModel!.Category == null)
                        {
                            _skipCategoryChange = true;
                            this.ViewModel!.Category = this.ViewModel!.Categories?.FirstOrDefault(c => c.Id == this.ViewModel!.SubCategory.CategoryId);
                            _skipCategoryChange = false;
                        }

                        SumPicker.Focus();
                    }
                });

                RxApp.MainThreadScheduler.Schedule(TimeSpan.FromSeconds(1), () => sumFocused = false);
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
        var subCategory = item as BaseSubCategoryViewModel;

        if (subCategory != null)
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

}
