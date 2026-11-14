using FamilyMoney.Configuration;
using FamilyMoney.Sync;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FamilyMoney.Tests;

public sealed class FolderSyncObjectStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "FamilyMoneyFolderSyncTests",
        Guid.NewGuid().ToString("N"));

    public FolderSyncObjectStoreTests()
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
    public async Task CreateOnly_AndIfMatch_ProtectTheManifest()
    {
        var store = CreateStore();
        const string key = "familymoney/9d1f6a4e/deltas/000001.json";

        await store.UploadTextAsync(key, "one", SyncWriteCondition.CreateOnly);

        await Assert.ThrowsAsync<SyncConflictException>(
            () => store.UploadTextAsync(key, "two", SyncWriteCondition.CreateOnly));

        var (content, etag) = await store.TryDownloadTextAsync(key);
        Assert.Equal("one", content);
        Assert.False(string.IsNullOrWhiteSpace(etag));

        await store.UploadTextAsync(key, "two", SyncWriteCondition.IfMatch(etag!));

        await Assert.ThrowsAsync<SyncConflictException>(
            () => store.UploadTextAsync(key, "three", SyncWriteCondition.IfMatch(etag!)));

        Assert.Equal("two", (await store.TryDownloadTextAsync(key)).Content);
    }

    [Fact]
    public async Task FilesAndStreams_RoundTrip()
    {
        var store = CreateStore();
        var source = Path.Combine(_root, "source.bin");
        var restored = Path.Combine(_root, "restored.bin");
        await File.WriteAllBytesAsync(source, [1, 2, 3, 4]);

        await store.UploadFileAsync("images/a", source);
        Assert.True(await store.ExistsAsync("images/a"));

        await store.DownloadFileAsync("images/a", restored);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(restored));

        await using var uploaded = new MemoryStream([5, 6, 7]);
        await store.UploadStreamAsync("images/b", uploaded, "b.bin");

        await using var downloaded = await store.DownloadStreamAsync("images/b");
        Assert.NotNull(downloaded);
        using var memory = new MemoryStream();
        await downloaded!.CopyToAsync(memory);
        Assert.Equal(new byte[] { 5, 6, 7 }, memory.ToArray());

        await store.DeleteAsync("images/a");
        Assert.False(await store.ExistsAsync("images/a"));
        Assert.Null(await store.DownloadStreamAsync("images/a"));
    }

    [Fact]
    public async Task PathOutsideRoot_IsRejected()
    {
        var store = CreateStore();

        await Assert.ThrowsAsync<ArgumentException>(() => store.ExistsAsync("../outside.json"));
    }

    private FolderSyncObjectStore CreateStore() => new(
        new FolderStorageConfiguration { Path = _root },
        NullLogger<FolderSyncObjectStore>.Instance);
}
