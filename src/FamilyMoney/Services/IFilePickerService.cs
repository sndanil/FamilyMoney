namespace FamilyMoney.Services;

public interface IFilePickerService
{
    Task<Stream?> PickCsvAsync(CancellationToken cancellationToken = default);

    Task<string?> PickSaveDatabaseFileAsync(string? suggestedFileName = null, CancellationToken cancellationToken = default);

    Task<string?> PickDatabaseFileAsync(CancellationToken cancellationToken = default);

    Task<string?> PickFolderAsync(CancellationToken cancellationToken = default);
}
