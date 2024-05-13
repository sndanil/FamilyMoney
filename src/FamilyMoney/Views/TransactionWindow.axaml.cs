using Avalonia;
using Avalonia.ReactiveUI;
using FamilyMoney.Utils;
using FamilyMoney.ViewModels;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;


namespace FamilyMoney.Views
{
    public partial class TransactionWindow : ReactiveWindow<BaseTransactionViewModel>
    {
        private bool _skipFirst = true;
        private bool _sumFocused = false;

        public TransactionWindow()
        {
            InitializeComponent();

            this.WhenActivated(action => action(ViewModel!.OkCommand.Subscribe(Close)));
            this.WhenActivated(action => action(ViewModel!.CancelCommand.Subscribe(Close)));

            this.WhenActivated(disposables =>
            {
                this.WhenAnyValue(v => v.ViewModel!.Sum)
                    .Do(v =>
                    {
                        if (!_skipFirst)
                        {
                            this.ViewModel!.ToSum = this.ViewModel!.Sum;
                        }
                    })
                    .Subscribe();

                this.WhenAnyValue(v => v.ViewModel!.Category)
                    .Do(v =>
                    {
                        if (!_skipFirst)
                        {
                            this.ViewModel!.SubCategoryText = null;
                            this.ViewModel!.SubCategory = null;
                        }
                    })
                    .Subscribe();

                this.WhenAnyValue(v => v.ViewModel!.SubCategory)
                    .Do(v =>
                    {
                        if (!_skipFirst && this.ViewModel!.Sum == 0)
                        {
                            this.ViewModel!.Sum = 10;
                        }
                    })
                    .Subscribe();

                RxApp.MainThreadScheduler.Schedule(TimeSpan.FromSeconds(1), () =>
                {
                    _skipFirst = false;
                });
            });

            this.WhenActivated(disposables =>
            {
            });

            this.Activated += TransactionWindowActivated;

            SubCategoryCompleteBox.DropDownClosed += SubCategoryCompleteBoxDropDownClosed; ;
            SubCategoryCompleteBox.ItemSelector = ItemSelector;
            SubCategoryCompleteBox.ItemFilter = ItemFilter;
        }

        private void SubCategoryCompleteBoxDropDownClosed(object? sender, EventArgs e)
        {
            if (!_sumFocused)
            {
                _sumFocused = true;
                RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(50), () => {
                    if (SubCategoryCompleteBox.SelectedItem != null)
                        SumPicker.Focus();
                });
                RxApp.MainThreadScheduler.Schedule(TimeSpan.FromSeconds(1), () => _sumFocused = false);
            }
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
    }
}
