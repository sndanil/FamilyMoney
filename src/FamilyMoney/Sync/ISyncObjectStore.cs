namespace FamilyMoney.Sync;

public interface ISyncObjectStore
{
    /// <summary>Поддерживает ли хранилище условную запись (If-Match / If-None-Match).</summary>
    bool SupportsConditionalWrite { get; }

    /// <summary>Список объектов под префиксом. Нужен для журналов по устройствам и снапшотов.</summary>
    Task<IReadOnlyList<SyncObjectInfo>> ListAsync(
        string prefix,
        CancellationToken cancellationToken = default);

    Task<(string? Content, string? ETag)> TryDownloadTextAsync(string key, CancellationToken cancellationToken = default);

    Task UploadTextAsync(
        string key,
        string content,
        SyncWriteCondition condition,
        CancellationToken cancellationToken = default);

    Task UploadFileAsync(string key, string localFilePath, CancellationToken cancellationToken = default);

    Task DownloadFileAsync(string key, string localFilePath, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    Task UploadStreamAsync(string key, Stream stream, string fileName, CancellationToken cancellationToken = default);

    Task<Stream?> DownloadStreamAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
