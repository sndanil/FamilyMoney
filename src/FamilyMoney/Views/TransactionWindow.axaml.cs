using Avalonia.ReactiveUI;
using FamilyMoney.ViewModels;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;


namespace FamilyMoney.Views
{
    public partial class TransactionWindow : ReactiveWindow<BaseTransactionViewModel>
    {
        private readonly Dictionary<char, char> _keyboard = new() 
        {
            { 'q', 'é' },
            { 'w', 'ö' },
            { 'e', 'ó' },
            { 'r', 'ê' },
            { 't', 'å' },
            { 'y', 'í' },
            { 'u', 'ã' },
            { 'i', 'ø' },
            { 'o', 'ù' },
            { 'p', 'ç' },
            { '[', 'õ' },
            { ']', 'ú' },
            { 'a', 'ô' },
            { 's', 'û' },
            { 'd', 'â' },
            { 'f', 'à' },
            { 'g', 'ï' },
            { 'h', 'ð' },
            { 'j', 'î' },
            { 'k', 'ë' },
            { 'l', 'ä' },
            { ';', 'æ' },
            { ':', 'æ' },
            { '\'', 'ý' },
            { '"', 'ý' },
            { 'z', 'ÿ' },
            { 'x', '÷' },
            { 'c', 'ñ' },
            { 'v', 'ì' },
            { 'b', 'è' },
            { 'n', 'ò' },
            { 'm', 'ü' },
            { ',', 'á' },
            { '.', 'þ' },
            { '/', '.' },
            { '<', 'á' },
            { '>', 'þ' },
            { '?', '.' },
        };

        public TransactionWindow()
        {
            InitializeComponent();

            this.WhenActivated(action => action(ViewModel!.OkCommand.Subscribe(Close)));
            this.WhenActivated(action => action(ViewModel!.CancelCommand.Subscribe(Close)));

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
            else if (ViewModel?.Sum == 0m)
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
                        || subCategory.Name?.Contains(Translate(search), StringComparison.OrdinalIgnoreCase) == true)
                        && (transaction?.Category == null || subCategory.CategoryId == transaction?.Category.Id);
                }

                return transaction?.Category == null || transaction?.Category.Id == subCategory.CategoryId;
            }

            return true;
        }

        private string Translate(string str)
        {
            return new string(str.ToLower().Select(c => 
            {
                char result;
                return _keyboard.TryGetValue(c, out result) ? result : c;
            }).ToArray());
        }
    }
}
