using Avalonia.Controls;
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
    }

    private async void ChangeImageButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var files = await topLevel!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выбор изображения",
            AllowMultiple = false,
            FileTypeFilter = new [] { new("Изображения") { Patterns = new[] { "*.png", "*.jpg" }, MimeTypes = new[] { "*/*" } }, FilePickerFileTypes.All }
        });

        if (files.Any())
        {
            await using var stream = await files.Single().OpenReadAsync();
            var account = (AccountViewModel?)this.DataContext;
            account!.Name = files.Single().Name;
            account!.Image = Bitmap.DecodeToWidth(stream, 400);
        }
    }
}