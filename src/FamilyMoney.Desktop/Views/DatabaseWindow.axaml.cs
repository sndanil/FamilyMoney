using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Messages;
using FamilyMoney.ViewModels.Settings;
using System.Linq;

namespace FamilyMoney;

public partial class DatabaseWindow : Window
{
    public DatabaseWindow()
    {
        InitializeComponent();

        WeakReferenceMessenger.Default.Register<DatabaseWindow, ModelCloseMessage<DatabaseViewModel>>(this, static (w, m) => w.Close(m.Result));
    }

    private async void BrowseDatabasePath(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not DatabaseViewModel database)
        {
            return;
        }

        var topLevel = GetTopLevel(this);
        var file = await topLevel!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Файл базы данных",
            DefaultExtension = "db",
            FileTypeChoices =
            [
                new("База LiteDB") { Patterns = ["*.db"] },
            ],
            SuggestedFileName = "database.db",
        });

        if (file != null)
        {
            database.Path = file.Path.LocalPath;
        }
    }

    private async void BrowseBackupsFolder(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not DatabaseViewModel database)
        {
            return;
        }

        var topLevel = GetTopLevel(this);
        var folders = await topLevel!.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Папка для резервных копий",
            AllowMultiple = false,
        });

        if (folders.Any())
        {
            database.BackupsFolder = folders.Single().Path.LocalPath;
        }
    }
}
