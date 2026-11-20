using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Messages;
using FamilyMoney.Navigation.ViewModels;
using FamilyMoney.Utils;
using FamilyMoney.ViewModels;
using System.IO;
using System.Linq;

namespace FamilyMoney.Views;

public partial class AccountWindow : Window
{
    public AccountWindow()
    {
        InitializeComponent();

        WeakReferenceMessenger.Default.Register<AccountWindow, ModelCloseMessage<AccountViewModel>>(this, static (w, m) => w.Close(m.Result));

        ImageControl.AddHandler(DragDrop.DropEvent, DropImage);
    }

    private void DropImage(object? sender, DragEventArgs e)
    {
        var file = e.DataTransfer.TryGetFiles()?.FirstOrDefault();

        if (file == null || DataContext is not AccountViewModel account)
        {
            return;
        }

        var path = file.TryGetLocalPath();
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        using var stream = File.OpenRead(path);
        account.ImageData = ImageConverter.ToImageData(stream);
        account.Icon = null;
        account.IconColor = null;
    }

    private async void ChangeImage(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not AccountViewModel account)
        {
            return;
        }

        var viewModel = new IconPickerViewModel(
            account.Name,
            true,
            account.Icon,
            account.IconColor,
            account.ImageData);

        var dialog = new IconPickerWindow { DataContext = viewModel };
        var result = await dialog.ShowDialog<IconSelection?>(this);
        if (result == null)
        {
            return;
        }

        account.Icon = result.Icon;
        account.IconColor = result.IconColor;
        account.ImageData = result.ImageData;
    }
}
