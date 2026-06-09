namespace FamilyMoney.Sync;

public interface ISyncObjectStore
{
    Task<(string? Content, string? ETag)> TryDownloadTextAsync(string key, CancellationToken cancellationToken = default);

    Task UploadTextAsync(string key, string content, string? ifMatchEtag, CancellationToken cancellationToken = default);

    Task UploadFileAsync(string key, string localFilePath, CancellationToken cancellationToken = default);

    Task DownloadFileAsync(string key, string localFilePath, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    Task UploadStreamAsync(string key, Stream stream, string fileName, CancellationToken cancellationToken = default);

    Task<Stream?> DownloadStreamAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
