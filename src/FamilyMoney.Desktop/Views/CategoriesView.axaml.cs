using Avalonia.Controls;
using Avalonia.Input;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Views;

public partial class CategoriesView : UserControl
{
    public CategoriesView()
    {
        InitializeComponent();
    }

    private void SubCategoryNameKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: BaseSubCategoryViewModel subCategory }
            || DataContext is not CategoriesViewModel viewModel)
        {
            return;
        }

        if (e.Key is Key.Enter or Key.Return)
        {
            viewModel.SaveRenameSubCategoryCommand.Execute(subCategory);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            viewModel.CancelRenameSubCategoryCommand.Execute(subCategory);
            e.Handled = true;
        }
    }
}
