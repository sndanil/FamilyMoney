using Avalonia.Controls;
using Avalonia.Interactivity;
using FamilyMoney.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyMoney.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private async void OnPeerSyncClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel)
        {
            return;
        }

        var host = AppInit.GlobalHost?.Services.GetService<PeerHostViewModel>();
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (host == null || owner == null)
        {
            return;
        }

        var window = new PeerSyncWindow { DataContext = host };
        await window.ShowDialog(owner);
    }
}