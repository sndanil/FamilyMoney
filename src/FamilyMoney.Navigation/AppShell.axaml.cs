using Avalonia.Controls;

namespace FamilyMoney.Navigation;

public partial class AppShell : UserControl
{
    public AppShell()
    {
        InitializeComponent();
    }

    public NavigationPage NavigationPage => ShellNavigation;
}
