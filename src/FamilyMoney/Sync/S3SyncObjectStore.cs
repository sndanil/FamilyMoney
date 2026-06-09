using Amazon.S3;
using FamilyMoney.Configuration;
using FluentStorage;
using FluentStorage.Blobs;
using Microsoft.Extensions.Logging;

namespace FamilyMoney.Sync;

public sealed class S3SyncObjectStore : ISyncObjectStore, IDisposable
{
    private readonly ILogger<S3SyncObjectStore> _logger;
    private readonly IBlobStorage _storage;

    public S3SyncObjectStore(S3StorageConfiguration configuration, ILogger<S3SyncObjectStore> logger)
    {
        _logger = logger;
        _storage = CreateBlobStorage(configuration);
    }

    public async Task<(string? Content, string? ETag)> TryDownloadTextAsync(string key, CancellationToken cancellationToken = default)
    {
        var blob = await _storage.GetBlobAsync(key, cancellationToken);
        if (blob == null)
        {
            return (null, null);
        }

        var content = await _storage.ReadTextAsync(key, cancellationToken: cancellationToken);
        return (content, GetETag(blob));
    }

    public async Task UploadTextAsync(string key, string content, string? ifMatchEtag, CancellationToken cancellationToken = default)
    {
        await _storage.WriteTextAsync(key, content, cancellationToken: cancellationToken);
        _logger.LogDebug("Uploaded S3 object {Key}", key);
    }

    public async Task UploadFileAsync(string key, string localFilePath, CancellationToken cancellationToken = default)
    {
        await _storage.WriteFileAsync(key, localFilePath, cancellationToken);
        _logger.LogDebug("Uploaded S3 file {Key}", key);
    }

    public async Task DownloadFileAsync(string key, string localFilePath, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(localFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await _storage.ReadToFileAsync(key, localFilePath, cancellationToken);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        _storage.ExistsAsync(key, cancellationToken);

    public async Task UploadStreamAsync(string key, Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        await _storage.WriteAsync(key, stream, cancellationToken: cancellationToken);
        _logger.LogDebug("Uploaded S3 stream {Key}", key);
    }

    public async Task<Stream?> DownloadStreamAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var stream = await _storage.OpenReadAsync(key, cancellationToken);
        if (stream == null)
        {
            return null;
        }

        var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;
        return memoryStream;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
        _storage.DeleteAsync(key, cancellationToken);

    public void Dispose() => _storage.Dispose();

    private static IBlobStorage CreateBlobStorage(S3StorageConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.ServiceUrl) && !configuration.ForcePathStyle)
        {
            var region = string.IsNullOrWhiteSpace(configuration.Region) ? "us-east-1" : configuration.Region;
            return StorageFactory.Blobs.AwsS3(
                configuration.AccessKey,
                configuration.SecretKey,
                sessionToken: null,
                configuration.Bucket,
                region,
                serviceUrl: null);
        }

        var config = new AmazonS3Config
        {
            ForcePathStyle = configuration.ForcePathStyle,
        };

        if (!string.IsNullOrWhiteSpace(configuration.ServiceUrl))
        {
            config.ServiceURL = configuration.ServiceUrl;
        }

        if (!string.IsNullOrWhiteSpace(configuration.Region))
        {
            config.AuthenticationRegion = configuration.Region;
        }

        return StorageFactory.Blobs.AwsS3(
            configuration.AccessKey,
            configuration.SecretKey,
            sessionToken: null,
            configuration.Bucket,
            config);
    }

    private static string? GetETag(Blob blob)
    {
        if (blob.Properties.TryGetValue("ETag", out var etag) && etag != null)
        {
            return etag.ToString();
        }

        return blob.MD5;
    }
}
