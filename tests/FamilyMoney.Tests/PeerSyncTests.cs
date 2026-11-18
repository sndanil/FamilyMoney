using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Models;
using FamilyMoney.Models.Sync;
using FamilyMoney.State;
using FamilyMoney.Sync;
using FamilyMoney.Sync.Peer;
using LiteDB;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using Xunit;

namespace FamilyMoney.Tests;

public sealed class PeerSyncTests : IDisposable
{
    private const string Token = "one-time-pairing-token";
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "familymoney-peer-" + Guid.NewGuid().ToString("N"));

    public PeerSyncTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task InitialSync_CopiesEntitiesAndImages()
    {
        var desktop = CreateNode("desktop");
        var phone = CreateNode("phone");
        var transport = new InMemoryTransport(desktop.Server);

        var account = TestData.Account(Guid.NewGuid(), "Семья");
        desktop.Repository.UpdateAccount(account);

        var transaction = TestData.Income(account.Id, DateTime.Today, 1250m);
        desktop.Repository.InsertTransactions([transaction]);

        using var image = new MemoryStream(Encoding.UTF8.GetBytes("jpeg-bytes"));
        desktop.Repository.UpdateImage(account.Id, "photo.jpg", image);

        var result = await phone.Client.SyncAsync(transport, Token);

        Assert.Contains(phone.Repository.GetAccounts(), item => item.Id == account.Id);
        Assert.NotNull(phone.Repository.GetTransaction(transaction.Id));
        Assert.Equal(2, result.ReceivedEntities);
        Assert.Equal(1, result.ReceivedImages);

        using var received = phone.Repository.TryGetImage(account.Id);
        Assert.NotNull(received);
        using var memory = new MemoryStream();
        received!.CopyTo(memory);
        Assert.Equal(Encoding.UTF8.GetBytes("jpeg-bytes"), memory.ToArray());
    }

    [Fact]
    public async Task PublisherServer_EnqueuesOnlyChangedRecords()
    {
        var desktop = CreateNode("desktop", publishReceivedChanges: true);
        var phone = CreateNode("phone");
        var transport = new InMemoryTransport(desktop.Server);

        var account = TestData.Account(Guid.NewGuid(), "С телефона через P2P");
        phone.Repository.UpdateAccount(account);
        var phoneLastChange = phone.Repository.GetAccount(account.Id).LastChange;

        await phone.Client.SyncAsync(transport, Token);

        var pending = desktop.Repository.GetPendingSyncChanges();
        var entry = Assert.Single(pending.Entities);
        Assert.Equal(account.Id, entry.EntityId);
        Assert.Equal(phoneLastChange, entry.LastChange);
        Assert.Contains(desktop.Repository.GetAccounts(), item => item.Id == account.Id);

        // Повторная сессия не должна создать второй outbox-элемент.
        await phone.Client.SyncAsync(transport, Token);
        Assert.Single(desktop.Repository.GetPendingSyncChanges().Entities);
    }

    [Fact]
    public async Task PublisherServer_DoesNotEnqueueUnchangedRecords()
    {
        var desktop = CreateNode("desktop", publishReceivedChanges: true);
        var phone = CreateNode("phone");
        var transport = new InMemoryTransport(desktop.Server);

        desktop.Repository.UpdateAccount(TestData.Account(Guid.NewGuid(), "С десктопа"));
        desktop.Repository.ClearPendingSyncChanges(desktop.Repository.GetPendingSyncChanges());

        await phone.Client.SyncAsync(transport, Token);

        Assert.False(desktop.Repository.GetPendingSyncChanges().HasChanges);
    }

    [Fact]
    public async Task PublisherServer_RelaysPhoneChangesToS3()
    {
        var desktop = CreateNode("desktop", publishReceivedChanges: true);
        var phone = CreateNode("phone");
        var store = new FakeSyncStore();

        desktop.Database.Sync = new SyncStorageConfiguration
        {
            Provider = SyncProvider.S3,
            Enabled = true,
            S3 = new S3StorageConfiguration
            {
                Enabled = true,
                Bucket = "bucket",
                AccessKey = "access",
                SecretKey = "secret",
            },
        };

        var configuration = new FakeConfiguration(desktop.Database);
        var cloudState = new LocalSyncStateStore(Path.Combine(_root, "desktop"));
        var syncService = new SyncService(
            configuration,
            desktop.Repository,
            new FakeStoreFactory(store),
            cloudState,
            new SyncImageSynchronizer(
                desktop.Repository,
                NullLogger<SyncImageSynchronizer>.Instance),
            NullLogger<SyncService>.Instance);

        var account = TestData.Account(Guid.NewGuid(), "Через D в облако");
        phone.Repository.UpdateAccount(account);

        await phone.Client.SyncAsync(new InMemoryTransport(desktop.Server), Token);
        Assert.True(desktop.Repository.GetPendingSyncChanges().HasChanges);

        var cloud = await syncService.SyncAsync();

        Assert.True(cloud.Success, cloud.Message);
        Assert.False(desktop.Repository.GetPendingSyncChanges().HasChanges);

        var manifest = store.GetText(SyncPaths.DeviceManifestKey(
            desktop.Database,
            cloudState.DeviceId));
        Assert.NotNull(manifest);
        var delta = store.GetText(SyncPaths.DeviceDeltaKey(
            desktop.Database,
            cloudState.DeviceId,
            1));
        Assert.NotNull(delta);
        Assert.Contains(account.Id.ToString(), delta, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublisherServer_RelaysDeletionEvenIfDesktopNeverHadRecord()
    {
        var desktop = CreateNode("desktop", publishReceivedChanges: true);
        var phone = CreateNode("phone");
        var transport = new InMemoryTransport(desktop.Server);

        var account = TestData.Account(Guid.NewGuid(), "Удалён на телефоне");
        phone.Repository.UpdateAccount(account);
        phone.Repository.DeleteAccount(account.Id);

        await phone.Client.SyncAsync(transport, Token);

        var pending = desktop.Repository.GetPendingSyncChanges();
        var entry = Assert.Single(pending.Entities);
        Assert.Equal(account.Id, entry.EntityId);
        Assert.NotNull(entry.DeletedAt);
        Assert.DoesNotContain(desktop.Repository.GetAccounts(), item => item.Id == account.Id);
    }

    [Fact]
    public async Task PublisherServer_EnqueuesImages()
    {
        var desktop = CreateNode("desktop", publishReceivedChanges: true);
        var phone = CreateNode("phone");
        var transport = new InMemoryTransport(desktop.Server);

        var account = TestData.Account(Guid.NewGuid(), "С фото");
        phone.Repository.UpdateAccount(account);
        using (var image = new MemoryStream([11, 12, 13]))
        {
            phone.Repository.UpdateImage(account.Id, "photo.jpg", image);
        }

        await phone.Client.SyncAsync(transport, Token);

        var pending = desktop.Repository.GetPendingSyncChanges();
        var imageEntry = Assert.Single(pending.Images);
        Assert.Equal(account.Id, imageEntry.EntityId);
        Assert.NotNull(desktop.Repository.TryGetImage(account.Id));
    }

    [Fact]
    public async Task IncrementalSync_GoesBothDirections()
    {
        var desktop = CreateNode("desktop");
        var phone = CreateNode("phone");
        var transport = new InMemoryTransport(desktop.Server);

        var shared = TestData.Account(Guid.NewGuid(), "Общий");
        desktop.Repository.UpdateAccount(shared);
        await phone.Client.SyncAsync(transport, Token);

        var phoneOnly = TestData.Account(Guid.NewGuid(), "С телефона");
        phone.Repository.UpdateAccount(phoneOnly);

        var changed = desktop.Repository.GetAccount(shared.Id);
        changed.Name = "Общий (обновлён)";
        desktop.Repository.UpdateAccount(changed);

        var result = await phone.Client.SyncAsync(transport, Token);

        Assert.Equal("Общий (обновлён)", phone.Repository.GetAccount(shared.Id).Name);
        Assert.Contains(desktop.Repository.GetAccounts(), item => item.Id == phoneOnly.Id);
        Assert.True(result.ReceivedEntities >= 1);
        Assert.True(result.SentEntities >= 1);
    }

    [Fact]
    public async Task DeletedEntity_IsPropagatedWithoutResurrection()
    {
        var desktop = CreateNode("desktop");
        var phone = CreateNode("phone");
        var transport = new InMemoryTransport(desktop.Server);

        var account = TestData.Account(Guid.NewGuid(), "Удаляемый");
        desktop.Repository.UpdateAccount(account);
        await phone.Client.SyncAsync(transport, Token);
        Assert.Contains(phone.Repository.GetAccounts(), item => item.Id == account.Id);

        desktop.Repository.DeleteAccount(account.Id);
        await phone.Client.SyncAsync(transport, Token);

        Assert.DoesNotContain(phone.Repository.GetAccounts(), item => item.Id == account.Id);
    }

    [Fact]
    public async Task ImageDelete_IsPropagated()
    {
        var desktop = CreateNode("desktop");
        var phone = CreateNode("phone");
        var transport = new InMemoryTransport(desktop.Server);

        var account = TestData.Account(Guid.NewGuid(), "С фото");
        desktop.Repository.UpdateAccount(account);
        using (var image = new MemoryStream([1, 2, 3, 4]))
        {
            desktop.Repository.UpdateImage(account.Id, "photo.jpg", image);
        }

        await phone.Client.SyncAsync(transport, Token);
        Assert.NotNull(phone.Repository.TryGetImage(account.Id));

        desktop.Repository.DeleteAccount(account.Id);
        await phone.Client.SyncAsync(transport, Token);

        Assert.Null(phone.Repository.TryGetImage(account.Id));
    }

    [Fact]
    public async Task PeerSync_DoesNotClearCloudOutbox()
    {
        var desktop = CreateNode("desktop");
        var phone = CreateNode("phone");
        var transport = new InMemoryTransport(desktop.Server);

        var account = TestData.Account(Guid.NewGuid(), "Локальное");
        phone.Repository.UpdateAccount(account);
        Assert.True(phone.Repository.GetPendingSyncChanges().HasChanges);
        Assert.False(desktop.Repository.GetPendingSyncChanges().HasChanges);

        await phone.Client.SyncAsync(transport, Token);

        Assert.True(phone.Repository.GetPendingSyncChanges().HasChanges);
        Assert.False(desktop.Repository.GetPendingSyncChanges().HasChanges);
        Assert.Contains(desktop.Repository.GetAccounts(), item => item.Id == account.Id);

        var repeated = await phone.Client.SyncAsync(transport, Token);

        // Запись на самой отметке может переотправиться/вернуться: это
        // идемпотентно, счётчики не обязаны быть нулевыми.
        Assert.True(repeated.SentEntities <= 1);
        Assert.True(repeated.ReceivedEntities <= 1);
    }

    [Fact]
    public async Task TwoPhones_ConvergeThroughDesktop()
    {
        var desktop = CreateNode("desktop");
        var first = CreateNode("first");
        var second = CreateNode("second");
        var firstTransport = new InMemoryTransport(desktop.Server);
        var secondTransport = new InMemoryTransport(desktop.Server);

        var fromFirst = TestData.Account(Guid.NewGuid(), "Первый");
        first.Repository.UpdateAccount(fromFirst);
        await first.Client.SyncAsync(firstTransport, Token);

        await second.Client.SyncAsync(secondTransport, Token);
        Assert.Contains(second.Repository.GetAccounts(), item => item.Id == fromFirst.Id);

        var fromSecond = TestData.Account(Guid.NewGuid(), "Второй");
        second.Repository.UpdateAccount(fromSecond);
        await second.Client.SyncAsync(secondTransport, Token);

        await first.Client.SyncAsync(firstTransport, Token);
        Assert.Contains(first.Repository.GetAccounts(), item => item.Id == fromSecond.Id);
    }

    [Fact]
    public async Task PagedPull_CopiesAllRecordsWithoutLooping()
    {
        var desktop = CreateNode("desktop");
        var phone = CreateNode("phone");
        var transport = new InMemoryTransport(desktop.Server);

        var accounts = Enumerable.Range(1, 3)
            .Select(index => TestData.Account(Guid.NewGuid(), $"Счёт {index}"))
            .ToList();
        foreach (var account in accounts)
        {
            desktop.Repository.UpdateAccount(account);
        }

        var client = new PeerSyncClientEngine(
            phone.Database.SyncId,
            "phone",
            "phone",
            phone.Repository,
            phone.StateStore,
            maxRecords: 1);

        var result = await client.SyncAsync(transport, Token);

        Assert.Equal(3, result.ReceivedEntities);
        // Пустая база не выгружает обратно то, что только что получила.
        Assert.Equal(0, result.SentEntities);
        foreach (var account in accounts)
        {
            Assert.Contains(phone.Repository.GetAccounts(), item => item.Id == account.Id);
        }
    }

    [Fact]
    public async Task PagedPush_SendsAllRecordsWithoutLooping()
    {
        var desktop = CreateNode("desktop");
        var phone = CreateNode("phone");
        var transport = new InMemoryTransport(desktop.Server);

        var accounts = Enumerable.Range(1, 3)
            .Select(index => TestData.Account(Guid.NewGuid(), $"С телефона {index}"))
            .ToList();
        foreach (var account in accounts)
        {
            phone.Repository.UpdateAccount(account);
        }

        var client = new PeerSyncClientEngine(
            phone.Database.SyncId,
            "phone",
            "phone",
            phone.Repository,
            phone.StateStore,
            maxRecords: 1);

        var result = await client.SyncAsync(transport, Token);

        Assert.Equal(3, result.SentEntities);
        foreach (var account in accounts)
        {
            Assert.Contains(desktop.Repository.GetAccounts(), item => item.Id == account.Id);
        }
    }

    [Fact]
    public void WrongProtocol_IsRejected()
    {
        var desktop = CreateNode("desktop");
        var error = Assert.Throws<PeerSyncException>(() => desktop.Server.Hello(new PeerHello
        {
            Protocol = PeerProtocol.Version + 1,
            SyncId = desktop.Database.SyncId,
            DeviceId = "phone",
            DeviceName = "Телефон",
            Token = Token,
        }));

        Assert.Equal(PeerErrorCodes.Protocol, error.Code);
    }

    [Fact]
    public async Task ImageFailure_DoesNotAdvanceWatermarkAndRetryWorks()
    {
        var desktop = CreateNode("desktop");
        var phone = CreateNode("phone");
        var transport = new InMemoryTransport(desktop.Server) { FailImages = true };

        var account = TestData.Account(Guid.NewGuid(), "С картинкой");
        desktop.Repository.UpdateAccount(account);
        using (var image = new MemoryStream([7, 8, 9]))
        {
            desktop.Repository.UpdateImage(account.Id, "photo.jpg", image);
        }

        var error = await Assert.ThrowsAsync<PeerSyncException>(
            () => phone.Client.SyncAsync(transport, Token));
        Assert.Equal(PeerErrorCodes.Image, error.Code);
        Assert.Equal(DateTime.MinValue, phone.StateStore
            .Load(phone.Database.SyncId, "desktop")
            .AppliedFromPeer);

        transport.FailImages = false;
        await phone.Client.SyncAsync(transport, Token);

        Assert.Contains(phone.Repository.GetAccounts(), item => item.Id == account.Id);
        Assert.NotNull(phone.Repository.TryGetImage(account.Id));
    }

    [Fact]
    public async Task SyncIdMismatch_IsRejected()
    {
        var desktop = CreateNode("desktop");
        var phone = CreateNode("phone", syncId: Guid.NewGuid());
        var transport = new InMemoryTransport(desktop.Server);

        var error = await Assert.ThrowsAsync<PeerSyncException>(
            () => phone.Client.SyncAsync(transport, Token));

        Assert.Equal(PeerErrorCodes.BadSyncId, error.Code);
    }

    [Fact]
    public async Task ExpiredOrWrongToken_IsRejected()
    {
        var desktop = CreateNode("desktop");
        var phone = CreateNode("phone");
        var transport = new InMemoryTransport(desktop.Server);

        var wrongToken = await Assert.ThrowsAsync<PeerSyncException>(
            () => phone.Client.SyncAsync(transport, "wrong"));
        Assert.Equal(PeerErrorCodes.BadToken, wrongToken.Code);

        var expired = CreateNode("expired", expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        var expiredPhone = CreateNode("expired-phone");
        var expiredError = await Assert.ThrowsAsync<PeerSyncException>(
            () => expiredPhone.Client.SyncAsync(new InMemoryTransport(expired.Server), Token));
        Assert.Equal(PeerErrorCodes.Expired, expiredError.Code);
    }

    [Fact]
    public void SyncChangesSince_IncludesTombstonesImagesAndPaging()
    {
        var node = CreateNode("query");
        var account = TestData.Account(Guid.NewGuid(), "Запрос");
        var second = TestData.Account(Guid.NewGuid(), "Второй запрос");
        node.Repository.UpdateAccount(account);
        node.Repository.UpdateAccount(second);

        using (var image = new MemoryStream([1, 2, 3]))
        {
            node.Repository.UpdateImage(account.Id, "photo.jpg", image);
        }

        var all = node.Repository.GetSyncChangesSince(DateTime.MinValue, 100);
        Assert.Contains(all.Changes.Entities, entry => entry.EntityId == account.Id);
        Assert.Contains(all.Changes.Images, entry => entry.EntityId == account.Id);

        var firstPage = node.Repository.GetSyncChangesSince(DateTime.MinValue, 1);
        Assert.Single(firstPage.Changes.Entities);
        Assert.True(firstPage.HasMore);

        var seenEntities = new HashSet<Guid>();
        var seenImages = new HashSet<Guid>();
        var since = DateTime.MinValue;
        while (true)
        {
            var page = node.Repository.GetSyncChangesSince(since, 1);
            seenEntities.UnionWith(page.Changes.Entities.Select(entry => entry.EntityId));
            seenImages.UnionWith(page.Changes.Images.Select(entry => entry.EntityId));
            if (!page.HasMore)
            {
                break;
            }

            since = page.Next;
        }

        Assert.Contains(account.Id, seenEntities);
        Assert.Contains(second.Id, seenEntities);
        Assert.Contains(account.Id, seenImages);

        node.Repository.DeleteAccount(account.Id);
        var deleted = node.Repository.GetSyncChangesSince(DateTime.MinValue, 100);
        Assert.Contains(deleted.Changes.Entities, entry =>
            entry.EntityId == account.Id && entry.DeletedAt != null);
    }

    [Fact]
    public async Task ClientRestart_ResumesFromSavedWatermarks()
    {
        var desktop = CreateNode("desktop");
        var phone = CreateNode("phone");
        var account = TestData.Account(Guid.NewGuid(), "До перезапуска");
        desktop.Repository.UpdateAccount(account);

        await phone.Client.SyncAsync(new InMemoryTransport(desktop.Server), Token);

        var restarted = new PeerSyncClientEngine(
            phone.Database.SyncId,
            "phone",
            "phone",
            phone.Repository,
            phone.StateStore);
        var result = await restarted.SyncAsync(new InMemoryTransport(desktop.Server), Token);

        Assert.True(result.SentEntities <= 1);
        Assert.True(result.ReceivedEntities <= 1);
    }

    [Fact]
    public void SyncChangesSince_IncludesCategoriesAndSubCategories()
    {
        var node = CreateNode("types");
        var category = new DebetCategory
        {
            Id = Guid.NewGuid(),
            Name = "Еда",
        };
        var subCategory = new DebetSubCategory
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Name = "Кафе",
        };
        node.Repository.UpdateCategroty(category);
        node.Repository.UpdateSubCategory(subCategory);

        var batch = node.Repository.GetSyncChangesSince(DateTime.MinValue, 100);

        Assert.Contains(batch.Changes.Entities, entry => entry.EntityId == category.Id);
        Assert.Contains(batch.Changes.Entities, entry => entry.EntityId == subCategory.Id);
    }

    [Fact]
    public void UpdateDbSchema_BackfillsLegacyImageMeta()
    {
        var node = CreateNode("legacy-image");
        var account = TestData.Account(Guid.NewGuid(), "Старое фото");
        node.Repository.UpdateAccount(account);

        using (var db = new LiteDatabase(node.Database.GetResolvedPath()))
        {
            db.FileStorage.Upload(
                account.Id.ToString(),
                "old.jpg",
                new MemoryStream([4, 5, 6]));
        }

        node.Repository.UpdateDbSchema();
        var batch = node.Repository.GetSyncChangesSince(DateTime.MinValue, 100);

        Assert.Contains(batch.Changes.Images, entry => entry.EntityId == account.Id);
    }

    [Fact]
    public void PeerStateStore_RoundTrips()
    {
        var syncId = Guid.NewGuid();
        var store = new PeerSyncStateStore(_root);
        var state = new PeerSyncState
        {
            DatabaseSyncId = syncId,
            PeerDeviceId = "phone",
            PeerName = "Телефон",
            AppliedFromPeer = new DateTime(2026, 11, 14, 17, 0, 0, DateTimeKind.Utc),
            PeerAppliedFromUs = new DateTime(2026, 11, 14, 17, 5, 0, DateTimeKind.Utc),
            LastSyncAt = new DateTime(2026, 11, 14, 17, 5, 1, DateTimeKind.Utc),
        };

        store.Save(state);
        var loaded = store.Load(syncId, "phone");

        Assert.Equal("Телефон", loaded.PeerName);
        Assert.Equal(state.AppliedFromPeer, loaded.AppliedFromPeer);
        Assert.Equal(state.PeerAppliedFromUs, loaded.PeerAppliedFromUs);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    private PeerNode CreateNode(
        string name,
        Guid? syncId = null,
        DateTimeOffset? expiresAt = null,
        bool publishReceivedChanges = false)
    {
        var home = Path.Combine(_root, name);
        Directory.CreateDirectory(home);

        var database = new DatabaseConfiguration
        {
            SyncId = syncId ?? new Guid("11111111-1111-1111-1111-111111111111"),
            Name = name,
            Path = Path.Combine(home, "database.db"),
            BackupsFolder = "Backups",
        };

        var configuration = new FakeConfiguration(database);
        var repository = new LiteDbRepository(
            configuration,
            new AccountLocalSettingsStore(configuration),
            NullLogger<LiteDbRepository>.Instance);
        repository.UpdateDbSchema();

        var stateStore = new PeerSyncStateStore(home);
        var deviceId = name.Replace("-", string.Empty, StringComparison.Ordinal);
        var server = new PeerSyncServerEngine(
            database.SyncId,
            "desktop",
            "Ноутбук",
            Token,
            expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(10),
            repository,
            stateStore,
            publishReceivedChanges: publishReceivedChanges);
        var client = new PeerSyncClientEngine(
            database.SyncId,
            deviceId,
            name,
            repository,
            stateStore);

        return new PeerNode(database, repository, server, client, stateStore);
    }

    private sealed record PeerNode(
        DatabaseConfiguration Database,
        LiteDbRepository Repository,
        PeerSyncServerEngine Server,
        PeerSyncClientEngine Client,
        PeerSyncStateStore StateStore);

    private sealed class InMemoryTransport : IPeerTransport
    {
        private readonly PeerSyncServerEngine _server;

        public InMemoryTransport(PeerSyncServerEngine server)
        {
            _server = server;
        }

        public bool FailImages { get; set; }

        public Task<PeerHelloAck> HelloAsync(
            PeerHello hello,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_server.Hello(hello));

        public Task<PeerChangesResponse> PullAsync(
            PeerPullRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_server.Pull(request));

        public Task<PeerPushAck> PushAsync(
            PeerPushRequest request,
            Func<SyncImageRecord, CancellationToken, Task<Stream?>> openImage,
            CancellationToken cancellationToken = default) =>
            _server.PushAsync(request, openImage, cancellationToken);

        public Task<Stream?> DownloadImageAsync(
            SyncImageRecord image,
            CancellationToken cancellationToken = default) =>
            FailImages
                ? Task.FromResult<Stream?>(null)
                : _server.DownloadImageAsync(image, cancellationToken);
    }
}
