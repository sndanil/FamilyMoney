using Avalonia.Controls;
using Avalonia.Interactivity;
using FamilyMoney.ViewModels;

namespace FamilyMoney.Navigation.Pages;

public partial class MenuPage : ContentPage
{
    public MenuPage()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private async void OnHomeClick(object? sender, RoutedEventArgs e)
    {
        if (Navigation is null)
        {
            return;
        }

        await Navigation.PopToRootAsync();
    }

    private async void OnCategoriesClick(object? sender, RoutedEventArgs e)
    {
        if (Navigation is null)
        {
            return;
        }

        await Navigation.PushAsync(new CategoriesPage { DataContext = ViewModel });
    }

    private async void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (Navigation is null)
        {
            return;
        }

        await Navigation.PushAsync(new SettingsPage { DataContext = ViewModel });
    }
}
