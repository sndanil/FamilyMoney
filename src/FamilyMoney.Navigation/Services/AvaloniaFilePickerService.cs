using Avalonia.Controls;
using Avalonia.Platform.Storage;
using FamilyMoney.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FamilyMoney.Navigation.Services;

/// <summary>
/// Файловые диалоги Avalonia. На Linux они идут через XDG-портал
/// (org.freedesktop.portal.FileChooser): если портала в системе нет, диалог
/// не открывается, и <see cref="IsAvailable"/> позволяет сказать об этом заранее.
/// Ошибку самого диалога не роняем — считаем, что выбор отменён, и пишем в журнал.
/// </summary>
public sealed class AvaloniaFilePickerService(
    Func<TopLevel?> topLevelProvider,
    ILogger<AvaloniaFilePickerService>? logger = null) : IFilePickerService
{
    public bool IsAvailable => Provider is { CanOpen: true, CanSave: true };

    private IStorageProvider? Provider => topLevelProvider()?.StorageProvider;

    public Task<string?> PickSaveDatabaseFileAsync(string? suggestedFileName = null, CancellationToken cancellationToken = default)
        => SaveAsync(provider => provider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Файл базы данных",
            DefaultExtension = "db",
            FileTypeChoices =
            [
                new FilePickerFileType("База LiteDB") { Patterns = ["*.db"] },
            ],
            SuggestedFileName = suggestedFileName ?? "database.db",
        }));

    public Task<string?> PickSaveBackupFileAsync(string suggestedDirectory, string suggestedFileName, CancellationToken cancellationToken = default)
        => SaveAsync(async provider => await provider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Резервная копия",
            DefaultExtension = "db",
            SuggestedStartLocation = await TryGetFolderAsync(provider, suggestedDirectory),
            SuggestedFileName = suggestedFileName,
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                new FilePickerFileType("Резервная копия LiteDB") { Patterns = ["*.db"] },
            ],
        }));

    public Task<string?> PickBackupFileAsync(string suggestedDirectory, CancellationToken cancellationToken = default)
        => OpenFileAsync(async provider => await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Восстановление из резервной копии",
            AllowMultiple = false,
            SuggestedStartLocation = await TryGetFolderAsync(provider, suggestedDirectory),
            FileTypeFilter =
            [
                new FilePickerFileType("Резервная копия LiteDB") { Patterns = ["*.db"] },
            ],
        }));

    public Task<string?> PickDatabaseFileAsync(CancellationToken cancellationToken = default)
        => OpenFileAsync(provider => provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Файл базы данных",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("База LiteDB") { Patterns = ["*.db"] },
            ],
        }));

    public Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
        => OpenFolderAsync(provider => provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Папка для резервных копий",
            AllowMultiple = false,
        }));

    private async Task<string?> SaveAsync(Func<IStorageProvider, Task<IStorageFile?>> pick)
    {
        var provider = Provider;
        if (provider is not { CanSave: true })
        {
            return null;
        }

        try
        {
            var file = await pick(provider);
            return file?.TryGetLocalPath();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "Диалог сохранения файла не открылся");
            return null;
        }
    }

    private async Task<string?> OpenFileAsync(Func<IStorageProvider, Task<IReadOnlyList<IStorageFile>>> pick)
    {
        var provider = Provider;
        if (provider is not { CanOpen: true })
        {
            return null;
        }

        try
        {
            var files = await pick(provider);
            return files.FirstOrDefault()?.TryGetLocalPath();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "Диалог выбора файла не открылся");
            return null;
        }
    }

    private async Task<string?> OpenFolderAsync(Func<IStorageProvider, Task<IReadOnlyList<IStorageFolder>>> pick)
    {
        var provider = Provider;
        if (provider is not { CanOpen: true })
        {
            return null;
        }

        try
        {
            var folders = await pick(provider);
            return folders.FirstOrDefault()?.TryGetLocalPath();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "Диалог выбора папки не открылся");
            return null;
        }
    }

    private async Task<IStorageFolder?> TryGetFolderAsync(IStorageProvider provider, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return await provider.TryGetFolderFromPathAsync(path);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
