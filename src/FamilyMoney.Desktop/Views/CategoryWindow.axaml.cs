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

        using var stream = File.OpenRead(file.Path.LocalPath);
        category.ImageData = ImageConverter.ToImageData(stream);
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

        if (files.Any() && DataContext is BaseCategoryViewModel category)
        {
            var file = files.Single();
            await using var stream = await file.OpenReadAsync();
            category.ImageData = ImageConverter.ToImageData(stream);
        }
    }
}
