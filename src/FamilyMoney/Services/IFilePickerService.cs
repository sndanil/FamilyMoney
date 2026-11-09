namespace FamilyMoney.Services;

public interface IFilePickerService
{
    Task<string?> PickSaveDatabaseFileAsync(string? suggestedFileName = null, CancellationToken cancellationToken = default);

    Task<string?> PickSaveBackupFileAsync(string suggestedDirectory, string suggestedFileName, CancellationToken cancellationToken = default);

    Task<string?> PickBackupFileAsync(string suggestedDirectory, CancellationToken cancellationToken = default);

    Task<string?> PickDatabaseFileAsync(CancellationToken cancellationToken = default);

    Task<string?> PickFolderAsync(CancellationToken cancellationToken = default);
}
