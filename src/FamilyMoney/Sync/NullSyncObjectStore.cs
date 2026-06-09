namespace FamilyMoney.Sync;

public sealed class NullSyncObjectStore : ISyncObjectStore
{
    public Task<(string? Content, string? ETag)> TryDownloadTextAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<(string?, string?)>((null, null));

    public Task UploadTextAsync(string key, string content, string? ifMatchEtag, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task UploadFileAsync(string key, string localFilePath, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task DownloadFileAsync(string key, string localFilePath, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task UploadStreamAsync(string key, Stream stream, string fileName, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<Stream?> DownloadStreamAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream?>(null);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
