using FamilyMoney.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace FamilyMoney.Sync;

/// <summary>
/// Хранилище синхронизации в обычном каталоге: локальном, сетевом или
/// синхронизируемом сторонним клиентом (Syncthing, Яндекс.Диск, OneDrive).
/// Каталог содержит только объекты обмена, но не живой файл базы.
/// </summary>
public sealed class FolderSyncObjectStore : ISyncObjectStore
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _root;
    private readonly ILogger<FolderSyncObjectStore> _logger;

    public FolderSyncObjectStore(
        FolderStorageConfiguration configuration,
        ILogger<FolderSyncObjectStore> logger)
    {
        if (string.IsNullOrWhiteSpace(configuration.Path))
        {
            throw new ArgumentException("Не задан каталог синхронизации.", nameof(configuration));
        }

        var root = configuration.Path.Trim();
        if (!Path.IsPathRooted(root))
        {
            root = Path.Combine(GlobalConfiguration.HomeFolder, root);
        }

        _root = Path.GetFullPath(root);
        _logger = logger;
    }

    public bool SupportsConditionalWrite => true;

    public Task<IReadOnlyList<SyncObjectInfo>> ListAsync(
        string prefix,
        CancellationToken cancellationToken = default)
    {
        var directory = ResolvePath(prefix);
        if (!Directory.Exists(directory))
        {
            return Task.FromResult<IReadOnlyList<SyncObjectInfo>>([]);
        }

        var result = new List<SyncObjectInfo>();
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(_root, file).Replace('\\', '/');
            var info = new FileInfo(file);
            result.Add(new SyncObjectInfo(
                relative,
                info.Length,
                null,
                new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero)));
        }

        return Task.FromResult<IReadOnlyList<SyncObjectInfo>>(result);
    }

    public async Task<(string? Content, string? ETag)> TryDownloadTextAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        if (!File.Exists(path))
        {
            return (null, null);
        }

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        return (Utf8NoBom.GetString(bytes), ComputeETag(bytes));
    }

    public async Task UploadTextAsync(
        string key,
        string content,
        SyncWriteCondition condition,
        CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        EnsureParent(path);

        if (condition.IfNoneMatch)
        {
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await using (var stream = new FileStream(
                                 temp,
                                 FileMode.CreateNew,
                                 FileAccess.Write,
                                 FileShare.None,
                                 bufferSize: 16 * 1024,
                                 useAsync: true))
                {
                    var bytes = Utf8NoBom.GetBytes(content);
                    await stream.WriteAsync(bytes, cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                }

                try
                {
                    File.Move(temp, path);
                }
                catch (IOException) when (File.Exists(path))
                {
                    throw new SyncConflictException($"Объект {key} уже существует.");
                }

                _logger.LogDebug("Created folder sync object {Key}", key);
                return;
            }
            finally
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(condition.IfMatchEtag))
        {
            var current = File.Exists(path)
                ? ComputeETag(await File.ReadAllBytesAsync(path, cancellationToken))
                : null;

            if (!string.Equals(current, condition.IfMatchEtag, StringComparison.Ordinal))
            {
                throw new SyncConflictException($"Объект {key} уже изменён другим устройством.");
            }
        }

        await WriteAtomicAsync(path, Utf8NoBom.GetBytes(content), cancellationToken);
        _logger.LogDebug("Uploaded folder sync object {Key}", key);
    }

    public async Task UploadFileAsync(
        string key,
        string localFilePath,
        CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        EnsureParent(path);

        await using var source = new FileStream(
            localFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);
        await WriteAtomicAsync(path, source, cancellationToken);
        _logger.LogDebug("Uploaded folder sync file {Key}", key);
    }

    public async Task DownloadFileAsync(
        string key,
        string localFilePath,
        CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Объект синхронизации не найден.", key);
        }

        var directory = Path.GetDirectoryName(localFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var source = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);
        await using var destination = new FileStream(
            localFilePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            useAsync: true);
        await source.CopyToAsync(destination, cancellationToken);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.Exists(ResolvePath(key)));

    public async Task UploadStreamAsync(
        string key,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        EnsureParent(path);

        await WriteAtomicAsync(path, stream, cancellationToken);
        _logger.LogDebug("Uploaded folder sync stream {Key}", key);
    }

    public async Task<Stream?> DownloadStreamAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        if (!File.Exists(path))
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        return new MemoryStream(bytes);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private async Task WriteAtomicAsync(
        string path,
        byte[] content,
        CancellationToken cancellationToken)
    {
        using var source = new MemoryStream(content, writable: false);
        await WriteAtomicAsync(path, source, cancellationToken);
    }

    private async Task WriteAtomicAsync(
        string path,
        Stream content,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(
                             temp,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 64 * 1024,
                             useAsync: true))
            {
                await content.CopyToAsync(stream, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }

            throw;
        }
    }

    private string ResolvePath(string key)
    {
        var normalized = key.Replace('\\', '/').TrimStart('/');
        var relative = normalized.Replace('/', Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(_root, relative));

        var rootPrefix = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!full.StartsWith(rootPrefix, comparison)
            && !string.Equals(full, _root, comparison))
        {
            throw new ArgumentException("Ключ синхронизации выходит за пределы каталога.", nameof(key));
        }

        return full;
    }

    private static void EnsureParent(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string ComputeETag(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content));
}
