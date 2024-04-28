using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using FamilyMoney.ViewModels;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;

namespace FamilyMoney.Views;

public partial class AccountWindow : ReactiveWindow<AccountViewModel>
{
    public AccountWindow()
    {
        InitializeComponent();

        this.WhenActivated(action => action(ViewModel!.OkCommand.Subscribe(Close)));
        this.WhenActivated(action => action(ViewModel!.CancelCommand.Subscribe(Close)));

        ImageControl.AddHandler(DragDrop.DropEvent, DropImage);
    }

    private void DropImage(object? sender, DragEventArgs e)
    {
        var file = e.Data.GetFiles()?.FirstOrDefault();

        if (file == null || DataContext is not AccountViewModel account)
            return;

        using var stream = File.OpenRead(file.Path.LocalPath);
        try
        {
            account.Image = Bitmap.DecodeToWidth(stream, 400);
        }
        catch { }
    }

    private async void ChangeImage(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        var files = await topLevel!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выбор изображения",
            AllowMultiple = false,
            FileTypeFilter = [
                new ("Изображения") { Patterns = [ "*.png", "*.jpg" ], MimeTypes = [ "*/*" ] },
                    FilePickerFileTypes.All
                ]
        });

        if (files.Any() && DataContext is AccountViewModel account)
        {
            var file = files.Single();
            await using var stream = await file.OpenReadAsync();
            account.Image = Bitmap.DecodeToWidth(stream, 400);
        }
    }
}