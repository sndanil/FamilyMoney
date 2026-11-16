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

public partial class CategoryWindow : Window
{
    public CategoryWindow()
    {
        InitializeComponent();

        WeakReferenceMessenger.Default.Register<CategoryWindow, ModelCloseMessage<BaseCategoryViewModel>>(this, static (w, m) => w.Close(m.Result));

        ImageControl.AddHandler(DragDrop.DropEvent, DropImage);
    }

    private void DropImage(object? sender, DragEventArgs e)
    {
        var file = e.DataTransfer.TryGetFiles()?.FirstOrDefault();

        if (file == null || DataContext is not BaseCategoryViewModel category)
        {
            return;
        }

        var path = file.TryGetLocalPath();
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        using var stream = File.OpenRead(path);
        category.ImageData = ImageConverter.ToImageData(stream);
    }

    private async void ChangeImage(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var provider = GetTopLevel(this)?.StorageProvider;
        if (provider is not { CanOpen: true })
        {
            return;
        }

        try
        {
            var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Выбор изображения",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new ("Изображения") { Patterns = [ "*.png", "*.jpg" ], MimeTypes = [ "*/*" ] },
                    FilePickerFileTypes.All
                ]
            });

            if (files.Any() && DataContext is BaseCategoryViewModel category)
            {
                var file = files.Single();
                await using var stream = await file.OpenReadAsync();
                category.ImageData = ImageConverter.ToImageData(stream);
            }
        }
        catch (System.Exception)
        {
            // Диалог недоступен (например, нет xdg-desktop-portal) — просто не меняем картинку.
        }
    }
}
