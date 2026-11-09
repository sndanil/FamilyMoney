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

    public async Task<string?> PickSaveBackupFileAsync(string suggestedDirectory, string suggestedFileName, CancellationToken cancellationToken = default)
    {
        var topLevel = topLevelProvider();
        if (topLevel is null)
        {
            return null;
        }

        var folder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(suggestedDirectory);
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Резервная копия",
            DefaultExtension = "db",
            SuggestedStartLocation = folder,
            SuggestedFileName = suggestedFileName,
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                new FilePickerFileType("Резервная копия LiteDB") { Patterns = ["*.db"] },
            ],
        });

        return file?.Path.LocalPath;
    }

    public async Task<string?> PickBackupFileAsync(string suggestedDirectory, CancellationToken cancellationToken = default)
    {
        var topLevel = topLevelProvider();
        if (topLevel is null)
        {
            return null;
        }

        var folder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(suggestedDirectory);
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Восстановление из резервной копии",
            AllowMultiple = false,
            SuggestedStartLocation = folder,
            FileTypeFilter =
            [
                new FilePickerFileType("Резервная копия LiteDB") { Patterns = ["*.db"] },
            ],
        });

        return files.FirstOrDefault()?.Path.LocalPath;
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
