using Avalonia.Controls;
using Avalonia.Input;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Navigation.Controls;

public partial class AccountsDrawerView : UserControl
{
    public AccountsDrawerView()
    {
        InitializeComponent();
    }

    private void OnAccountTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: AccountViewModel account })
        {
            account.SelectCommand();
        }
    }
}
