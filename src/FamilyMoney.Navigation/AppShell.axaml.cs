using Avalonia.Controls;
using Avalonia.Threading;

namespace FamilyMoney.Navigation;

public partial class AppShell : UserControl
{
    public AppShell()
    {
        InitializeComponent();

        // NavigationPage / DrawerPage re-host their child pages, assigning the
        // inherited DataContext before the page bindings are instanced. As a result
        // the bindings never observe the shell view model. Re-applying the
        // DataContext once the page is in the visual tree forces the bindings to
        // re-evaluate against the correct context.
        ShellHome.AttachedToVisualTree += (_, _) => RefreshDataContext(ShellHome);
        ShellMenu.AttachedToVisualTree += (_, _) => RefreshDataContext(ShellMenu);
        DataContextChanged += (_, _) =>
        {
            RefreshDataContext(ShellHome);
            RefreshDataContext(ShellMenu);
        };
    }

    public NavigationPage NavigationPage => ShellNavigation;

    public DrawerPage DrawerPage => ShellDrawer;

    private void RefreshDataContext(Control page)
    {
        Dispatcher.UIThread.Post(() =>
        {
            page.DataContext = null;
            page.DataContext = DataContext;
        }, DispatcherPriority.Loaded);
    }
}
