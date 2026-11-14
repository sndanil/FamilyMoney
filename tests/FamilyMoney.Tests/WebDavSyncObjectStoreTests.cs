using FamilyMoney.Configuration;
using FamilyMoney.Sync;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Xunit;

namespace FamilyMoney.Tests;

public sealed class WebDavSyncObjectStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "FamilyMoneyWebDavTests",
        Guid.NewGuid().ToString("N"));

    public WebDavSyncObjectStoreTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task CreateOnly_AndIfMatch_UseConditionalHeaders()
    {
        var handler = new FakeWebDavHandler();
        var store = CreateStore(handler);
        const string key = "familymoney/9d1f6a4e/manifest.json";

        await store.UploadTextAsync(key, "one", SyncWriteCondition.CreateOnly);
        Assert.Contains(
            "remote.php/dav/files/user/familymoney/9d1f6a4e",
            handler.CreatedCollections);

        await Assert.ThrowsAsync<SyncConflictException>(
            () => store.UploadTextAsync(key, "two", SyncWriteCondition.CreateOnly));

        var (content, etag) = await store.TryDownloadTextAsync(key);
        Assert.Equal("one", content);
        Assert.False(string.IsNullOrWhiteSpace(etag));

        await store.UploadTextAsync(key, "two", SyncWriteCondition.IfMatch(etag!));
        await Assert.ThrowsAsync<SyncConflictException>(
            () => store.UploadTextAsync(key, "three", SyncWriteCondition.IfMatch(etag!)));
    }

    [Fact]
    public async Task FilesAndStreams_RoundTrip()
    {
        var handler = new FakeWebDavHandler();
        var store = CreateStore(handler);
        var source = Path.Combine(_root, "source.bin");
        var restored = Path.Combine(_root, "restored.bin");
        await File.WriteAllBytesAsync(source, [1, 2, 3, 4]);

        await store.UploadFileAsync("familymoney/9d1f6a4e/images/a", source);
        Assert.True(await store.ExistsAsync("familymoney/9d1f6a4e/images/a"));

        await store.DownloadFileAsync("familymoney/9d1f6a4e/images/a", restored);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(restored));

        await using var uploaded = new MemoryStream([5, 6, 7]);
        await store.UploadStreamAsync("familymoney/9d1f6a4e/images/b", uploaded, "b.bin");

        await using var downloaded = await store.DownloadStreamAsync("familymoney/9d1f6a4e/images/b");
        Assert.NotNull(downloaded);
        using var memory = new MemoryStream();
        await downloaded!.CopyToAsync(memory);
        Assert.Equal(new byte[] { 5, 6, 7 }, memory.ToArray());

        await store.DeleteAsync("familymoney/9d1f6a4e/images/a");
        Assert.False(await store.ExistsAsync("familymoney/9d1f6a4e/images/a"));
        Assert.Null(await store.DownloadStreamAsync("familymoney/9d1f6a4e/images/a"));
    }

    private WebDavSyncObjectStore CreateStore(HttpMessageHandler handler) => new(
        new WebDavStorageConfiguration
        {
            Url = "https://dav.test/remote.php/dav/files/user",
            UserName = "user",
            Password = "secret",
        },
        NullLogger<WebDavSyncObjectStore>.Instance,
        new HttpClient(handler));

    private sealed class FakeWebDavHandler : HttpMessageHandler
    {
        private const string RootPath = "remote.php/dav/files/user";

        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
        private readonly HashSet<string> _collections = new(StringComparer.Ordinal)
        {
            RootPath,
        };

        public List<string> CreatedCollections { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = Normalize(request.RequestUri!);
            var method = request.Method.Method;

            if (method == "MKCOL")
            {
                if (_collections.Contains(path))
                {
                    return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
                }

                _collections.Add(path);
                CreatedCollections.Add(path);
                return new HttpResponseMessage(HttpStatusCode.Created);
            }

            if (request.Method == HttpMethod.Put)
            {
                if (!ParentCollectionExists(path))
                {
                    return new HttpResponseMessage(HttpStatusCode.Conflict);
                }

                var bytes = request.Content == null
                    ? []
                    : await request.Content.ReadAsByteArrayAsync(cancellationToken);
                var exists = _files.ContainsKey(path);

                if (request.Headers.TryGetValues("If-None-Match", out var noneMatch)
                    && noneMatch.Any(value => value.Contains('*'))
                    && exists)
                {
                    return new HttpResponseMessage(HttpStatusCode.PreconditionFailed);
                }

                if (request.Headers.TryGetValues("If-Match", out var ifMatch)
                    && ifMatch.Any())
                {
                    var expected = TrimETag(ifMatch.First());
                    if (!exists || !string.Equals(CurrentETag(path), expected, StringComparison.Ordinal))
                    {
                        return new HttpResponseMessage(HttpStatusCode.PreconditionFailed);
                    }
                }

                _files[path] = bytes;
                var created = new HttpResponseMessage(HttpStatusCode.Created);
                created.Headers.ETag = new EntityTagHeaderValue($"\"{CurrentETag(path)}\"");
                return created;
            }

            if (request.Method == HttpMethod.Get)
            {
                if (!_files.TryGetValue(path, out var bytes))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(bytes),
                };
                response.Headers.ETag = new EntityTagHeaderValue($"\"{CurrentETag(path)}\"");
                return response;
            }

            if (request.Method == HttpMethod.Head)
            {
                if (!_files.ContainsKey(path))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Headers.ETag = new EntityTagHeaderValue($"\"{CurrentETag(path)}\"");
                return response;
            }

            if (request.Method == HttpMethod.Delete)
            {
                _files.Remove(path);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
        }

        private string CurrentETag(string path) =>
            Convert.ToHexString(SHA256.HashData(_files[path]));

        private bool ParentCollectionExists(string path)
        {
            var index = path.LastIndexOf('/');
            if (index < 0)
            {
                return false;
            }

            var parent = path[..index];
            return _collections.Contains(parent);
        }

        private static string Normalize(Uri uri) =>
            Uri.UnescapeDataString(uri.AbsolutePath).Trim('/');

        private static string TrimETag(string etag) => etag.Trim().Trim('"');
    }
}
