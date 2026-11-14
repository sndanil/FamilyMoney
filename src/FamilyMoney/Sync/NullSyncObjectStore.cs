namespace FamilyMoney.Sync;

public sealed class NullSyncObjectStore : ISyncObjectStore
{
    public bool SupportsConditionalWrite => false;

    public Task<IReadOnlyList<SyncObjectInfo>> ListAsync(
        string prefix,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SyncObjectInfo>>([]);

    public Task<(string? Content, string? ETag)> TryDownloadTextAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<(string?, string?)>((null, null));

    public Task UploadTextAsync(
        string key,
        string content,
        SyncWriteCondition condition,
        CancellationToken cancellationToken = default) =>
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
