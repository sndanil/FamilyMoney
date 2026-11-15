using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Models.Sync;
using FamilyMoney.State;
using FamilyMoney.Sync;
using FamilyMoney.Sync.Peer;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;
using Xunit;

namespace FamilyMoney.Tests;

public sealed class PeerTlsTests : IDisposable
{
    private const string Token = "tls-pairing-token";
    private static readonly Guid SyncId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "familymoney-tls-" + Guid.NewGuid().ToString("N"));

    public PeerTlsTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task LoopbackTls_SyncsDataAndImages()
    {
        var desktop = CreateNode("desktop");
        var phone = CreateNode("phone");
        var account = TestData.Account(Guid.NewGuid(), "По TLS");
        desktop.Repository.UpdateAccount(account);
        var transaction = TestData.Income(account.Id, DateTime.Today, 300m);
        desktop.Repository.InsertTransactions([transaction]);
        using (var image = new MemoryStream(Encoding.UTF8.GetBytes("tls-image")))
        {
            desktop.Repository.UpdateImage(account.Id, "photo.jpg", image);
        }

        await using var server = new PeerSyncServer(desktop.Server, IPAddress.Loopback);
        var endpoint = server.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var acceptTask = server.AcceptOnceAsync(timeout.Token);

        PeerSyncClient client;
        try
        {
            client = await PeerSyncClient.ConnectAsync(endpoint, timeout.Token);
        }
        catch (Exception ex)
        {
            var failed = await acceptTask;
            throw new InvalidOperationException("server: " + failed.Error, ex);
        }

        var result = await phone.Client.SyncAsync(client, Token, cancellationToken: timeout.Token);
        await client.DisposeAsync();
        var session = await acceptTask;

        Assert.True(session.Success, session.Error);
        Assert.Contains(phone.Repository.GetAccounts(), item => item.Id == account.Id);
        Assert.NotNull(phone.Repository.GetTransaction(transaction.Id));
        using var received = phone.Repository.TryGetImage(account.Id);
        Assert.NotNull(received);
        Assert.True(result.ReceivedEntities >= 2);
        Assert.Equal(1, result.ReceivedImages);
    }

    [Fact]
    public async Task TlsPush_SendsPhoneImageToDesktop()
    {
        var desktop = CreateNode("desktop", publishReceivedChanges: true);
        var phone = CreateNode("phone");
        var account = TestData.Account(Guid.NewGuid(), "Фото с телефона");
        phone.Repository.UpdateAccount(account);
        using (var image = new MemoryStream([21, 22, 23]))
        {
            phone.Repository.UpdateImage(account.Id, "phone.jpg", image);
        }

        await using var server = new PeerSyncServer(desktop.Server, IPAddress.Loopback);
        var endpoint = server.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var acceptTask = server.AcceptOnceAsync(timeout.Token);

        var client = await PeerSyncClient.ConnectAsync(endpoint, timeout.Token);
        await phone.Client.SyncAsync(client, Token, cancellationToken: timeout.Token);
        await client.DisposeAsync();
        var session = await acceptTask;

        Assert.True(session.Success, session.Error);
        Assert.Single(desktop.Repository.GetPendingSyncChanges().Images);
        Assert.NotNull(desktop.Repository.TryGetImage(account.Id));
    }

    [Fact]
    public async Task WrongFingerprint_IsRejected()
    {
        var desktop = CreateNode("desktop");
        await using var server = new PeerSyncServer(desktop.Server, IPAddress.Loopback);
        var endpoint = server.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var acceptTask = server.AcceptOnceAsync(timeout.Token);

        var wrong = (endpoint.Fingerprint[0] == 'A' ? 'B' : 'A') + endpoint.Fingerprint[1..];
        var wrongEndpoint = endpoint with { Fingerprint = wrong };

        await Assert.ThrowsAnyAsync<Exception>(
            () => PeerSyncClient.ConnectAsync(wrongEndpoint, timeout.Token));

        var session = await acceptTask;
        Assert.False(session.Success);
    }

    [Fact]
    public async Task WrongToken_IsRejectedOverTls()
    {
        var desktop = CreateNode("desktop");
        var phone = CreateNode("phone");

        await using var server = new PeerSyncServer(desktop.Server, IPAddress.Loopback);
        var endpoint = server.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var acceptTask = server.AcceptOnceAsync(timeout.Token);

        var client = await PeerSyncClient.ConnectAsync(endpoint, timeout.Token);
        var error = await Assert.ThrowsAsync<PeerSyncException>(
            () => phone.Client.SyncAsync(client, "wrong-token", cancellationToken: timeout.Token));
        await client.DisposeAsync();
        var session = await acceptTask;

        Assert.Equal(PeerErrorCodes.BadToken, error.Code);
        Assert.False(session.Success);
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

    private TlsNode CreateNode(string name, bool publishReceivedChanges = false)
    {
        var home = Path.Combine(_root, name);
        Directory.CreateDirectory(home);

        var database = new DatabaseConfiguration
        {
            SyncId = SyncId,
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
        var server = new PeerSyncServerEngine(
            database.SyncId,
            "desktop",
            "Ноутбук",
            Token,
            DateTimeOffset.UtcNow.AddMinutes(10),
            repository,
            stateStore,
            publishReceivedChanges: publishReceivedChanges);
        var client = new PeerSyncClientEngine(
            database.SyncId,
            "phone",
            "Телефон",
            repository,
            stateStore);

        return new TlsNode(repository, server, client);
    }

    private sealed record TlsNode(
        LiteDbRepository Repository,
        PeerSyncServerEngine Server,
        PeerSyncClientEngine Client);
}
