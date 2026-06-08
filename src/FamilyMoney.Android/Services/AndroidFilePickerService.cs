using Avalonia.Controls;
using Avalonia.Platform.Storage;
using FamilyMoney.Services;
using System.Linq;

namespace FamilyMoney.Android.Services;

public sealed class AndroidFilePickerService(Func<TopLevel?> topLevelProvider) : IFilePickerService
{
    public async Task<string?> PickSaveDatabaseFileAsync(string? suggestedFileName = null, CancellationToken cancellationToken = default)
    {
        var topLevel = topLevelProvider();
        if (topLevel is null)
        {
            return null;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Файл базы данных",
            DefaultExtension = "db",
            FileTypeChoices =
            [
                new FilePickerFileType("База LiteDB") { Patterns = ["*.db"] },
            ],
            SuggestedFileName = suggestedFileName ?? "database.db",
        });

        return file?.Path.LocalPath;
    }

    public Task<string?> PickDatabaseFileAsync(CancellationToken cancellationToken = default)
    {
        return PickFilePathAsync(["*.db"]);
    }

    public async Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
    {
        var topLevel = topLevelProvider();
        if (topLevel is null)
        {
            return null;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Папка для резервных копий",
            AllowMultiple = false,
        });

        return folders.FirstOrDefault()?.Path.LocalPath;
    }

    private async Task<string?> PickFilePathAsync(string[] patterns)
    {
        var topLevel = topLevelProvider();
        if (topLevel is null)
        {
            return null;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Файл") { Patterns = patterns },
            ],
        });

        return files.FirstOrDefault()?.Path.LocalPath;
    }
}
