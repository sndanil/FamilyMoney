namespace FamilyMoney.Services;

public interface IFilePickerService
{
    /// <summary>
    /// Диалог доступен в текущем окружении. На Linux без xdg-desktop-portal
    /// файловые диалоги не открываются — об этом стоит сказать пользователю,
    /// а не молча игнорировать нажатие кнопки.
    /// </summary>
    bool IsAvailable { get; }

    Task<string?> PickSaveDatabaseFileAsync(string? suggestedFileName = null, CancellationToken cancellationToken = default);

    Task<string?> PickSaveBackupFileAsync(string suggestedDirectory, string suggestedFileName, CancellationToken cancellationToken = default);

    Task<string?> PickBackupFileAsync(string suggestedDirectory, CancellationToken cancellationToken = default);

    Task<string?> PickDatabaseFileAsync(CancellationToken cancellationToken = default);

    Task<string?> PickFolderAsync(CancellationToken cancellationToken = default);
}
