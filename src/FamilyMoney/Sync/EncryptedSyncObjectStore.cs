namespace FamilyMoney.Sync;

/// <summary>
/// Прозрачно шифрует содержимое объектов, оставляя ключи и листинг как есть.
/// Благодаря этому протоколу синхронизации не нужно знать о шифровании.
/// </summary>
public sealed class EncryptedSyncObjectStore : ISyncObjectStore
{
    private readonly ISyncObjectStore _inner;
    private readonly SyncCrypto _crypto;

    public EncryptedSyncObjectStore(ISyncObjectStore inner, SyncCrypto crypto)
    {
        _inner = inner;
        _crypto = crypto;
    }

    public bool SupportsConditionalWrite => _inner.SupportsConditionalWrite;

    public Task<IReadOnlyList<SyncObjectInfo>> ListAsync(
        string prefix,
        CancellationToken cancellationToken = default) =>
        _inner.ListAsync(prefix, cancellationToken);

    public async Task<(string? Content, string? ETag)> TryDownloadTextAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var (content, etag) = await _inner.TryDownloadTextAsync(key, cancellationToken);
        return content == null
            ? (null, etag)
            : (_crypto.DecryptText(content, key), etag);
    }

    public Task UploadTextAsync(
        string key,
        string content,
        SyncWriteCondition condition,
        CancellationToken cancellationToken = default) =>
        _inner.UploadTextAsync(
            key,
            _crypto.EncryptText(content, key),
            condition,
            cancellationToken);

    public async Task UploadFileAsync(
        string key,
        string localFilePath,
        CancellationToken cancellationToken = default)
    {
        var plain = await File.ReadAllBytesAsync(localFilePath, cancellationToken);
        var encrypted = _crypto.Encrypt(plain, key);
        var temp = TempPath();
        try
        {
            await File.WriteAllBytesAsync(temp, encrypted, cancellationToken);
            await _inner.UploadFileAsync(key, temp, cancellationToken);
        }
        finally
        {
            TryDeleteFile(temp);
        }
    }

    public async Task DownloadFileAsync(
        string key,
        string localFilePath,
        CancellationToken cancellationToken = default)
    {
        var encryptedPath = TempPath();
        try
        {
            await _inner.DownloadFileAsync(key, encryptedPath, cancellationToken);
            var encrypted = await File.ReadAllBytesAsync(encryptedPath, cancellationToken);
            var plain = _crypto.Decrypt(encrypted, key);

            var directory = Path.GetDirectoryName(localFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(localFilePath, plain, cancellationToken);
        }
        finally
        {
            TryDeleteFile(encryptedPath);
        }
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        _inner.ExistsAsync(key, cancellationToken);

    public async Task UploadStreamAsync(
        string key,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var encrypted = _crypto.Encrypt(memory.ToArray(), key);
        await _inner.UploadStreamAsync(
            key,
            new MemoryStream(encrypted, writable: false),
            fileName,
            cancellationToken);
    }

    public async Task<Stream?> DownloadStreamAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        await using var stream = await _inner.DownloadStreamAsync(key, cancellationToken);
        if (stream == null)
        {
            return null;
        }

        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var decrypted = _crypto.Decrypt(memory.ToArray(), key);
        return new MemoryStream(decrypted, writable: false);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
        _inner.DeleteAsync(key, cancellationToken);

    private static string TempPath() =>
        Path.Combine(
            Path.GetTempPath(),
            "familymoney-sync-" + Guid.NewGuid().ToString("N") + ".tmp");

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // Временный файл не должен ломать синхронизацию.
        }
    }
}
