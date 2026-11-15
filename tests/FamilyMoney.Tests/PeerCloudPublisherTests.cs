using FamilyMoney.Configuration;
using FamilyMoney.Models.Sync;
using FamilyMoney.Sync;
using FamilyMoney.Sync.Peer;
using Xunit;

namespace FamilyMoney.Tests;

public sealed class PeerCloudPublisherTests
{
    [Fact]
    public async Task DisabledPublisher_DoesNotCallCloud()
    {
        var repository = CreateRepository(pending: true);
        var sync = new FakeSyncService();
        var publisher = CreatePublisher(repository, sync, publish: false);

        var result = await publisher.PublishAsync();

        Assert.False(result.Attempted);
        Assert.Equal(0, sync.Calls);
    }

    [Fact]
    public async Task PendingChanges_ArePublished()
    {
        var repository = CreateRepository(pending: true);
        var sync = new FakeSyncService();
        var publisher = CreatePublisher(repository, sync, publish: true);

        var result = await publisher.PublishAsync();

        Assert.True(result.Attempted);
        Assert.True(result.Success);
        Assert.Equal(1, sync.Calls);
    }

    [Fact]
    public async Task EmptyOutbox_SkipsCloud()
    {
        var repository = CreateRepository(pending: false);
        var sync = new FakeSyncService();
        var publisher = CreatePublisher(repository, sync, publish: true);

        var result = await publisher.PublishAsync();

        Assert.False(result.Attempted);
        Assert.Equal(0, sync.Calls);
    }

    [Fact]
    public async Task SyncGate_AllowsOnlyOneOperation()
    {
        var gate = new SyncOperationGate();
        using var first = await gate.TryEnterAsync();
        Assert.NotNull(first);
        Assert.Null(await gate.TryEnterAsync());

        first.Dispose();
        using var second = await gate.TryEnterAsync();
        Assert.NotNull(second);
    }

    [Fact]
    public async Task CloudFailure_IsReportedButOutboxCanRetry()
    {
        var repository = CreateRepository(pending: true);
        var sync = new FakeSyncService
        {
            Result = SyncResult.Failed("S3 unavailable"),
        };
        var publisher = CreatePublisher(repository, sync, publish: true);

        var result = await publisher.PublishAsync();

        Assert.True(result.Attempted);
        Assert.False(result.Success);
        Assert.Equal("S3 unavailable", result.Message);
        Assert.True(repository.GetPendingSyncChanges().HasChanges);
    }

    private static PeerCloudPublisher CreatePublisher(
        FakeSyncRepository repository,
        FakeSyncService sync,
        bool publish)
    {
        var database = new DatabaseConfiguration
        {
            SyncId = Guid.NewGuid(),
            Name = "Publisher",
            Path = "database.db",
            Sync = new SyncStorageConfiguration
            {
                PublishPeerChangesToCloud = publish,
            },
        };

        return new PeerCloudPublisher(
            new FakeConfiguration(database),
            repository,
            sync);
    }

    private static FakeSyncRepository CreateRepository(bool pending)
    {
        var repository = new FakeSyncRepository();
        if (pending)
        {
            repository.Entities.Add(new SyncOutboxEntry
            {
                EntityType = "Account",
                EntityId = Guid.NewGuid(),
                LastChange = DateTime.UtcNow,
                DataJson = "{}",
            });
        }

        return repository;
    }

    private sealed class FakeSyncService : ISyncService
    {
        public bool IsEnabled { get; set; } = true;

        public int Calls { get; private set; }

        public SyncResult Result { get; set; } = SyncResult.Completed("ok", 1, 1);

        public Task<SyncResult> SyncAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }
}
