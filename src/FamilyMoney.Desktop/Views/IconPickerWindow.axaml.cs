using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FamilyMoney.Navigation.Services;
using FamilyMoney.Navigation.ViewModels;
using FamilyMoney.Utils;

namespace FamilyMoney.Views;

public partial class IconPickerWindow : Window
{
    public IconPickerWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (ViewModel is not { } vm)
        {
            return;
        }

        UpdateColorVisibility();

        var color = vm.SelectedColor ?? IconPalette.Colors[0];
        var index = vm.Colors.IndexOf(color);
        if (index >= 0)
        {
            ColorList.SelectedIndex = index;
        }
    }

    private IconPickerViewModel? ViewModel => DataContext as IconPickerViewModel;

    private void OnIconSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is { } vm && sender is ListBox { SelectedItem: IconChoice choice })
        {
            vm.SelectIcon(choice);
        }
    }

    private void OnPackSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is { } vm && sender is ListBox { SelectedItem: PackAssetView asset })
        {
            vm.SelectPack(asset);
            ColorList.SelectedItem = null;
        }
    }

    private void OnColorSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is { } vm && sender is ListBox { SelectedItem: string color })
        {
            // Первый цвет — акцент: храним null, чтобы работал цвет по умолчанию.
            vm.SelectColor(color.Equals(IconPalette.Colors[0], StringComparison.OrdinalIgnoreCase) ? null : color);
        }
    }

    private void OnTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (FileStatus != null)
        {
            FileStatus.IsVisible = false;
        }

        UpdateColorVisibility();

        if (Tabs?.SelectedIndex == 2
            && ViewModel is { PreviewIcon: null, PreviewImage: null } vm
            && !string.IsNullOrWhiteSpace(vm.LetterText))
        {
            vm.SelectLetter(vm.LetterText);
        }
    }

    private void UpdateColorVisibility()
    {
        if (ColorPanel != null)
        {
            ColorPanel.IsVisible = Tabs?.SelectedIndex is 0 or 2;
        }
    }

    private async void OnChooseFile(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        var provider = GetTopLevel(this)?.StorageProvider;
        if (provider is not { CanOpen: true })
        {
            ShowFileError("Диалог выбора файла недоступен.");
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
                    new("Изображения") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp"], MimeTypes = ["image/*"] },
                ],
            });

            var file = files.FirstOrDefault();
            if (file == null)
            {
                return;
            }

            await using var stream = await file.OpenReadAsync();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            var data = memory.ToArray();
            if (data.Length > 20 * 1024 * 1024)
            {
                ShowFileError("Файл больше 20 МБ. Выберите картинку поменьше.");
                return;
            }

            vm.SelectImage(data);
            ColorList.SelectedItem = null;
            FileStatus.IsVisible = false;
        }
        catch (Exception)
        {
            ShowFileError("Не удалось прочитать файл.");
        }
    }

    private void OnClear(object? sender, RoutedEventArgs e)
    {
        ViewModel?.Clear();
        ColorList.SelectedItem = null;
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        Close(ViewModel?.BuildResult());
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void ShowFileError(string message)
    {
        FileStatus.Text = message;
        FileStatus.IsVisible = true;
    }
}
