using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using FamilyMoney.Configuration;
using FluentStorage;
using FluentStorage.Blobs;
using Microsoft.Extensions.Logging;
using System.Net;

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

    public bool SupportsConditionalWrite => true;

    public async Task<IReadOnlyList<SyncObjectInfo>> ListAsync(
        string prefix,
        CancellationToken cancellationToken = default)
    {
        using var storage = CreateBlobStorage(_configuration);
        var blobs = await storage.ListAsync(
            new ListOptions
            {
                FolderPath = prefix,
                Recurse = true,
            },
            cancellationToken);

        // FluentStorage отдаёт полный путь с ведущим «/». Ключи протокола
        // синхронизации заданы относительно корня бакета, поэтому листинг
        // нормализуется: иначе манифесты устройств не находятся по префиксу.
        return blobs
            .Where(blob => blob.IsFile)
            .Select(blob => new SyncObjectInfo(
                SyncObjectKeys.Normalize(blob.FullPath),
                blob.Size ?? 0,
                GetETag(blob),
                blob.LastModificationTime))
            .ToList();
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

    public async Task UploadTextAsync(
        string key,
        string content,
        SyncWriteCondition condition,
        CancellationToken cancellationToken = default)
    {
        if (!condition.IsConditional)
        {
            using var storage = CreateBlobStorage(_configuration);
            await storage.WriteTextAsync(key, content, cancellationToken: cancellationToken);
            _logger.LogDebug("Uploaded S3 object {Key}", key);
            return;
        }

        try
        {
            using var client = CreateS3Client(_configuration);
            var request = new PutObjectRequest
            {
                BucketName = _configuration.Bucket,
                Key = key,
                ContentBody = content,
                ContentType = "application/json; charset=utf-8",
            };

            if (condition.IfNoneMatch)
            {
                request.IfNoneMatch = "*";
            }
            else if (!string.IsNullOrWhiteSpace(condition.IfMatchEtag))
            {
                request.IfMatch = QuoteETag(condition.IfMatchEtag);
            }

            await client.PutObjectAsync(request, cancellationToken);
            _logger.LogDebug("Uploaded S3 object {Key} with condition", key);
        }
        catch (AmazonS3Exception ex) when (IsPreconditionFailure(ex))
        {
            throw new SyncConflictException($"Объект {key} уже изменён другим устройством.");
        }
        catch (AmazonS3Exception ex) when (IsNotSupported(ex))
        {
            throw new SyncWriteNotSupportedException(
                $"Хранилище не поддерживает условную запись объекта {key}.");
        }
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

    private static AmazonS3Client CreateS3Client(S3StorageConfiguration configuration)
    {
        var credentials = new BasicAWSCredentials(configuration.AccessKey, configuration.SecretKey);
        var config = new AmazonS3Config
        {
            ForcePathStyle = configuration.ForcePathStyle,
        };

        if (!string.IsNullOrWhiteSpace(configuration.ServiceUrl))
        {
            config.ServiceURL = configuration.ServiceUrl;
            if (!string.IsNullOrWhiteSpace(configuration.Region))
            {
                config.AuthenticationRegion = configuration.Region;
            }
        }
        else
        {
            var region = string.IsNullOrWhiteSpace(configuration.Region) ? "us-east-1" : configuration.Region;
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(region);
        }

        return new AmazonS3Client(credentials, config);
    }

    private static bool IsPreconditionFailure(AmazonS3Exception exception) =>
        exception.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict;

    private static bool IsNotSupported(AmazonS3Exception exception) =>
        exception.StatusCode is HttpStatusCode.BadRequest
            or HttpStatusCode.MethodNotAllowed
            or HttpStatusCode.NotImplemented
        || string.Equals(exception.ErrorCode, "NotImplemented", StringComparison.OrdinalIgnoreCase)
        || string.Equals(exception.ErrorCode, "UnsupportedHeader", StringComparison.OrdinalIgnoreCase);

    private static string QuoteETag(string etag)
    {
        var value = etag.Trim();
        return value.StartsWith('"') && value.EndsWith('"') ? value : "\"" + value + "\"";
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
