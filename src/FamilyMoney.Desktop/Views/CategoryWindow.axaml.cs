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
        category.Icon = null;
        category.IconColor = null;
    }

    private async void ChangeImage(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not BaseCategoryViewModel category)
        {
            return;
        }

        var viewModel = new IconPickerViewModel(
            category.Name,
            false,
            category.Icon,
            category.IconColor,
            category.ImageData);

        var dialog = new IconPickerWindow { DataContext = viewModel };
        var result = await dialog.ShowDialog<IconSelection?>(this);
        if (result == null)
        {
            return;
        }

        category.Icon = result.Icon;
        category.IconColor = result.IconColor;
        category.ImageData = result.ImageData;
    }
}
