using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Messages;
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

        using var stream = File.OpenRead(file.Path.LocalPath);
        account.ImageData = ImageConverter.ToImageData(stream);
    }

    private async void ChangeImage(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        var files = await topLevel!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выбор изображения",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new ("Изображения") { Patterns = [ "*.png", "*.jpg" ], MimeTypes = [ "*/*" ] },
                FilePickerFileTypes.All
            ]
        });

        if (files.Any() && DataContext is AccountViewModel account)
        {
            var file = files.Single();
            await using var stream = await file.OpenReadAsync();
            account.ImageData = ImageConverter.ToImageData(stream);
        }
    }
}
