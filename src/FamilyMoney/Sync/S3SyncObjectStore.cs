using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using FamilyMoney.Configuration;
using Microsoft.Extensions.Logging;

namespace FamilyMoney.Sync;

public sealed class S3SyncObjectStore : ISyncObjectStore, IDisposable
{
    private readonly S3StorageConfiguration _configuration;
    private readonly ILogger<S3SyncObjectStore> _logger;
    private readonly IAmazonS3 _client;

    public S3SyncObjectStore(S3StorageConfiguration configuration, ILogger<S3SyncObjectStore> logger)
    {
        _configuration = configuration;
        _logger = logger;

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
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(configuration.Region);
        }

        _client = new AmazonS3Client(configuration.AccessKey, configuration.SecretKey, config);
    }

    public async Task<(string? Content, string? ETag)> TryDownloadTextAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetObjectAsync(_configuration.Bucket, key, cancellationToken);
            using var reader = new StreamReader(response.ResponseStream);
            var content = await reader.ReadToEndAsync(cancellationToken);
            return (content, response.ETag);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return (null, null);
        }
    }

    public async Task UploadTextAsync(string key, string content, string? ifMatchEtag, CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _configuration.Bucket,
            Key = key,
            ContentBody = content,
            ContentType = "application/json; charset=utf-8",
        };

        await _client.PutObjectAsync(request, cancellationToken);
        _logger.LogDebug("Uploaded S3 object {Key}", key);
    }

    public async Task UploadFileAsync(string key, string localFilePath, CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _configuration.Bucket,
            Key = key,
            FilePath = localFilePath,
            ContentType = "application/octet-stream",
        };

        await _client.PutObjectAsync(request, cancellationToken);
        _logger.LogDebug("Uploaded S3 file {Key}", key);
    }

    public async Task DownloadFileAsync(string key, string localFilePath, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(localFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var request = new GetObjectRequest
        {
            BucketName = _configuration.Bucket,
            Key = key,
        };

        using var response = await _client.GetObjectAsync(request, cancellationToken);
        await using var output = File.Create(localFilePath);
        await response.ResponseStream.CopyToAsync(output, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.GetObjectMetadataAsync(_configuration.Bucket, key, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task UploadStreamAsync(string key, Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _configuration.Bucket,
            Key = key,
            InputStream = stream,
            ContentType = ResolveContentType(fileName),
        };

        await _client.PutObjectAsync(request, cancellationToken);
        _logger.LogDebug("Uploaded S3 stream {Key}", key);
    }

    public async Task<Stream?> DownloadStreamAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetObjectAsync(_configuration.Bucket, key, cancellationToken);
            var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.DeleteObjectAsync(_configuration.Bucket, key, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
        }
    }

    private static string ResolveContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream",
        };
    }

    public void Dispose() => _client.Dispose();
}
