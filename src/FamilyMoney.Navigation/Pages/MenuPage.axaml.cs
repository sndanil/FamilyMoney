using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Navigation.Pages;

public partial class MenuPage : ContentPage
{
    public MenuPage()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private AppShell? Shell => this.FindAncestorOfType<AppShell>();

    private void CloseDrawer()
    {
        var drawer = Shell?.DrawerPage;
        if (drawer != null)
        {
            drawer.IsOpen = false;
        }
    }

    private async void OnHomeClick(object? sender, RoutedEventArgs e)
    {
        var navigation = Shell?.NavigationPage;
        if (navigation == null)
        {
            return;
        }

        await navigation.PopToRootAsync();
        CloseDrawer();
    }

    private async void OnCategoriesClick(object? sender, RoutedEventArgs e)
    {
        var navigation = Shell?.NavigationPage;
        if (navigation == null)
        {
            return;
        }

        if (navigation.CurrentPage is not CategoriesPage)
        {
            await navigation.PushAsync(new CategoriesPage { DataContext = ViewModel });
        }

        CloseDrawer();
    }

    private async void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        var navigation = Shell?.NavigationPage;
        if (navigation == null)
        {
            return;
        }

        if (navigation.CurrentPage is not SettingsPage)
        {
            await navigation.PushAsync(new SettingsPage { DataContext = ViewModel });
        }

        CloseDrawer();
    }
}
