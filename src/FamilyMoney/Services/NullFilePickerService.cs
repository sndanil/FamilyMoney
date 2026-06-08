namespace FamilyMoney.Services;

public sealed class NullFilePickerService : IFilePickerService
{
    public Task<Stream?> PickCsvAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Stream?>(null);
    }

    public Task<string?> PickSaveDatabaseFileAsync(string? suggestedFileName = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<string?> PickDatabaseFileAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }
}
