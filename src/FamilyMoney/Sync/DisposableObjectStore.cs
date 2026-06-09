namespace FamilyMoney.Sync;

internal sealed class DisposableObjectStore : ISyncObjectStore, IDisposable
{
    private readonly ISyncObjectStore _inner;
    private readonly IDisposable _disposable;

    public DisposableObjectStore(ISyncObjectStore inner, IDisposable disposable)
    {
        _inner = inner;
        _disposable = disposable;
    }

    public Task<(string? Content, string? ETag)> TryDownloadTextAsync(string key, CancellationToken cancellationToken = default) =>
        _inner.TryDownloadTextAsync(key, cancellationToken);

    public Task UploadTextAsync(string key, string content, string? ifMatchEtag, CancellationToken cancellationToken = default) =>
        _inner.UploadTextAsync(key, content, ifMatchEtag, cancellationToken);

    public Task UploadFileAsync(string key, string localFilePath, CancellationToken cancellationToken = default) =>
        _inner.UploadFileAsync(key, localFilePath, cancellationToken);

    public Task DownloadFileAsync(string key, string localFilePath, CancellationToken cancellationToken = default) =>
        _inner.DownloadFileAsync(key, localFilePath, cancellationToken);

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        _inner.ExistsAsync(key, cancellationToken);

    public Task UploadStreamAsync(string key, Stream stream, string fileName, CancellationToken cancellationToken = default) =>
        _inner.UploadStreamAsync(key, stream, fileName, cancellationToken);

    public Task<Stream?> DownloadStreamAsync(string key, CancellationToken cancellationToken = default) =>
        _inner.DownloadStreamAsync(key, cancellationToken);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
        _inner.DeleteAsync(key, cancellationToken);

    public void Dispose() => _disposable.Dispose();
}
