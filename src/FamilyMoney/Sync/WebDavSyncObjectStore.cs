using FamilyMoney.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace FamilyMoney.Sync;

/// <summary>
/// Хранилище синхронизации по WebDAV. Подходит для Облака Mail.ru,
/// Nextcloud/ownCloud, Яндекс.Диска (с подпиской) и домашних NAS.
/// </summary>
public sealed class WebDavSyncObjectStore : ISyncObjectStore
{
    private static readonly HttpClient SharedClient = new()
    {
        Timeout = TimeSpan.FromSeconds(120),
    };

    private readonly WebDavStorageConfiguration _configuration;
    private readonly ILogger<WebDavSyncObjectStore> _logger;
    private readonly HttpClient _client;
    private readonly Uri _baseUri;
    private readonly string? _authorization;
    private readonly HashSet<string> _knownCollections = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _collectionGate = new(1, 1);

    public WebDavSyncObjectStore(
        WebDavStorageConfiguration configuration,
        ILogger<WebDavSyncObjectStore> logger,
        HttpClient? client = null)
    {
        if (!configuration.IsConfigured)
        {
            throw new ArgumentException("Не задан корректный адрес WebDAV.", nameof(configuration));
        }

        _configuration = configuration;
        _logger = logger;
        _client = client ?? SharedClient;

        var url = configuration.Url.Trim();
        if (!url.EndsWith('/'))
        {
            url += "/";
        }

        _baseUri = new Uri(url, UriKind.Absolute);
        _authorization = string.IsNullOrWhiteSpace(configuration.UserName)
            ? null
            : Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{configuration.UserName}:{configuration.Password}"));
    }

    public bool SupportsConditionalWrite => true;

    public async Task<(string? Content, string? ETag)> TryDownloadTextAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, BuildUri(key));
        using var response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return (null, null);
        }

        EnsureSuccess(response, "GET", key);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return (content, GetETag(response));
    }

    public async Task UploadTextAsync(
        string key,
        string content,
        SyncWriteCondition condition,
        CancellationToken cancellationToken = default)
    {
        await EnsureParentCollectionsAsync(key, cancellationToken);

        using var request = CreateRequest(HttpMethod.Put, BuildUri(key));
        ApplyCondition(request, condition);
        request.Content = new StringContent(content, Encoding.UTF8, "application/json");

        using var response = await _client.SendAsync(request, cancellationToken);
        HandleWriteResponse(response, key);
        _logger.LogDebug("Uploaded WebDAV object {Key}", key);
    }

    public async Task UploadFileAsync(
        string key,
        string localFilePath,
        CancellationToken cancellationToken = default)
    {
        await EnsureParentCollectionsAsync(key, cancellationToken);

        await using var stream = new FileStream(
            localFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);

        using var request = CreateRequest(HttpMethod.Put, BuildUri(key));
        request.Content = new StreamContent(stream);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var response = await _client.SendAsync(request, cancellationToken);
        HandleWriteResponse(response, key);
        _logger.LogDebug("Uploaded WebDAV file {Key}", key);
    }

    public async Task DownloadFileAsync(
        string key,
        string localFilePath,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, BuildUri(key));
        using var response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException("Объект синхронизации не найден.", key);
        }

        EnsureSuccess(response, "GET", key);

        var directory = Path.GetDirectoryName(localFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = new FileStream(
            localFilePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            useAsync: true);
        await source.CopyToAsync(destination, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Head, BuildUri(key));
        using var response = await _client.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        if (response.StatusCode == HttpStatusCode.MethodNotAllowed)
        {
            // Некоторые WebDAV-серверы не поддерживают HEAD.
            using var get = CreateRequest(HttpMethod.Get, BuildUri(key));
            using var getResponse = await _client.SendAsync(
                get,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (getResponse.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }

            EnsureSuccess(getResponse, "GET", key);
            return true;
        }

        EnsureSuccess(response, "HEAD", key);
        return true;
    }

    public async Task UploadStreamAsync(
        string key,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        await EnsureParentCollectionsAsync(key, cancellationToken);

        using var request = CreateRequest(HttpMethod.Put, BuildUri(key));
        request.Content = new StreamContent(stream);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var response = await _client.SendAsync(request, cancellationToken);
        HandleWriteResponse(response, key);
        _logger.LogDebug("Uploaded WebDAV stream {Key}", key);
    }

    public async Task<Stream?> DownloadStreamAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, BuildUri(key));
        using var response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        EnsureSuccess(response, "GET", key);
        var memory = new MemoryStream();
        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        {
            await source.CopyToAsync(memory, cancellationToken);
        }

        memory.Position = 0;
        return memory;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, BuildUri(key));
        using var response = await _client.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        EnsureSuccess(response, "DELETE", key);
    }

    private async Task EnsureParentCollectionsAsync(string key, CancellationToken cancellationToken)
    {
        var segments = key.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= 1)
        {
            return;
        }

        await _collectionGate.WaitAsync(cancellationToken);
        try
        {
            var prefix = string.Empty;
            for (var index = 0; index < segments.Length - 1; index++)
            {
                prefix = prefix.Length == 0 ? segments[index] : prefix + "/" + segments[index];
                if (_knownCollections.Contains(prefix))
                {
                    continue;
                }

                using var request = CreateRequest(new HttpMethod("MKCOL"), BuildUri(prefix, directory: true));
                using var response = await _client.SendAsync(request, cancellationToken);

                if (response.StatusCode is HttpStatusCode.Created
                    or HttpStatusCode.OK
                    or HttpStatusCode.MethodNotAllowed
                    or HttpStatusCode.MovedPermanently
                    or HttpStatusCode.Found)
                {
                    _knownCollections.Add(prefix);
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    // Родительский каталог ещё не создан — попробуем следующий раз.
                    throw new HttpRequestException(
                        $"WebDAV не создал каталог {prefix}: {(int)response.StatusCode}.");
                }

                EnsureSuccess(response, "MKCOL", prefix);
                _knownCollections.Add(prefix);
            }
        }
        finally
        {
            _collectionGate.Release();
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, Uri uri)
    {
        var request = new HttpRequestMessage(method, uri);
        if (_authorization != null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", _authorization);
        }

        return request;
    }

    private Uri BuildUri(string key, bool directory = false)
    {
        var parts = key.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString);
        var relative = string.Join('/', parts);
        if (directory && relative.Length > 0)
        {
            relative += "/";
        }

        return new Uri(_baseUri, relative);
    }

    private static void ApplyCondition(HttpRequestMessage request, SyncWriteCondition condition)
    {
        if (condition.IfNoneMatch)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", "*");
        }
        else if (!string.IsNullOrWhiteSpace(condition.IfMatchEtag))
        {
            request.Headers.TryAddWithoutValidation("If-Match", QuoteETag(condition.IfMatchEtag));
        }
    }

    private static void HandleWriteResponse(HttpResponseMessage response, string key)
    {
        if (response.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict)
        {
            throw new SyncConflictException($"Объект {key} уже изменён другим устройством.");
        }

        if (response.StatusCode is HttpStatusCode.BadRequest
            or HttpStatusCode.MethodNotAllowed
            or HttpStatusCode.NotImplemented)
        {
            throw new SyncWriteNotSupportedException(
                $"WebDAV-сервер не поддерживает условную запись объекта {key}.");
        }

        EnsureSuccess(response, "PUT", key);
    }

    private static void EnsureSuccess(HttpResponseMessage response, string method, string key)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw new HttpRequestException(
            $"WebDAV {method} для {key} вернул {(int)response.StatusCode} {response.ReasonPhrase}.");
    }

    private static string? GetETag(HttpResponseMessage response)
    {
        var etag = response.Headers.ETag?.Tag;
        return string.IsNullOrWhiteSpace(etag) ? null : etag;
    }

    private static string QuoteETag(string etag)
    {
        var value = etag.Trim();
        return value.StartsWith('"') && value.EndsWith('"') ? value : "\"" + value + "\"";
    }
}
