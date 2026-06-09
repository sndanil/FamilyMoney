using Amazon.S3;
using FamilyMoney.Configuration;
using FluentStorage;
using FluentStorage.Blobs;
using Microsoft.Extensions.Logging;

namespace FamilyMoney.Sync;

public sealed class S3SyncObjectStore : ISyncObjectStore
{
    private readonly S3StorageConfiguration _configuration;
    private readonly ILogger<S3SyncObjectStore> _logger;

    public S3SyncObjectStore(S3StorageConfiguration configuration, ILogger<S3SyncObjectStore> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(string? Content, string? ETag)> TryDownloadTextAsync(string key, CancellationToken cancellationToken = default)
    {
        using var storage = CreateBlobStorage(_configuration);
        var blob = await storage.GetBlobAsync(key, cancellationToken);
        if (blob == null)
        {
            return (null, null);
        }

        var content = await storage.ReadTextAsync(key, cancellationToken: cancellationToken);
        return (content, GetETag(blob));
    }

    public async Task UploadTextAsync(string key, string content, string? ifMatchEtag, CancellationToken cancellationToken = default)
    {
        using var storage = CreateBlobStorage(_configuration);
        await storage.WriteTextAsync(key, content, cancellationToken: cancellationToken);
        _logger.LogDebug("Uploaded S3 object {Key}", key);
    }

    public async Task UploadFileAsync(string key, string localFilePath, CancellationToken cancellationToken = default)
    {
        using var storage = CreateBlobStorage(_configuration);
        await storage.WriteFileAsync(key, localFilePath, cancellationToken);
        _logger.LogDebug("Uploaded S3 file {Key}", key);
    }

    public async Task DownloadFileAsync(string key, string localFilePath, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(localFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var storage = CreateBlobStorage(_configuration);
        await storage.ReadToFileAsync(key, localFilePath, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        using var storage = CreateBlobStorage(_configuration);
        return await storage.ExistsAsync(key, cancellationToken);
    }

    public async Task UploadStreamAsync(string key, Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        using var storage = CreateBlobStorage(_configuration);
        await storage.WriteAsync(key, stream, cancellationToken: cancellationToken);
        _logger.LogDebug("Uploaded S3 stream {Key}", key);
    }

    public async Task<Stream?> DownloadStreamAsync(string key, CancellationToken cancellationToken = default)
    {
        using var storage = CreateBlobStorage(_configuration);
        await using var stream = await storage.OpenReadAsync(key, cancellationToken);
        if (stream == null)
        {
            return null;
        }

        var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        using var storage = CreateBlobStorage(_configuration);
        await storage.DeleteAsync(key, cancellationToken);
    }

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
