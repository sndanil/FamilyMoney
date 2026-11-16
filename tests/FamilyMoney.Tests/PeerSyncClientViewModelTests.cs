using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Services;
using FamilyMoney.State;
using FamilyMoney.Sync;
using FamilyMoney.Sync.Peer;
using FamilyMoney.ViewModels.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using Xunit;

namespace FamilyMoney.Tests;

public sealed class PeerSyncClientViewModelTests : IDisposable
{
    private const string Token = "client-viewmodel-token";
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "familymoney-client-vm-" + Guid.NewGuid().ToString("N"));

    public PeerSyncClientViewModelTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task ScannedQr_AdoptsSyncIdAndSyncs()
    {
        var syncId = Guid.NewGuid();
        var server = CreateServer(syncId);
        var account = TestData.Account(Guid.NewGuid(), "С сервера");
        server.Repository.UpdateAccount(account);

        await using var tlsServer = new PeerSyncServer(server.Engine, IPAddress.Loopback);
        var endpoint = tlsServer.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var acceptTask = tlsServer.AcceptOnceAsync(timeout.Token);

        var pairing = new PeerPairingPayload
        {
            SyncId = syncId,
            Host = endpoint.Host,
            Port = endpoint.Port,
            Fingerprint = endpoint.Fingerprint,
            Token = Token,
            DeviceId = "desktop",
            DeviceName = "Ноутбук",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
        }.ToJson();

        var phone = CreatePhone(syncId: Guid.Empty);
        var scanner = new FakeScanner(pairing);
        var viewModel = new PeerSyncClientViewModel(
            phone.Configuration,
            phone.Repository,
            phone.LocalState,
            scanner,
            new FakePermission(true));

        await viewModel.StartAsync();
        var session = await acceptTask;

        Assert.True(session.Success, session.Error);
        Assert.Equal(syncId, phone.Database.SyncId);
        Assert.Contains(phone.Repository.GetAccounts(), item => item.Id == account.Id);
        Assert.Contains("Получено", viewModel.Status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeniedLocalNetworkPermission_BlocksSync()
    {
        var phone = CreatePhone(syncId: Guid.NewGuid());
        var viewModel = new PeerSyncClientViewModel(
            phone.Configuration,
            phone.Repository,
            phone.LocalState,
            new FakeScanner("{}"),
            new FakePermission(false));

        await viewModel.StartAsync();

        Assert.Contains("разрешения", viewModel.Status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DifferentSyncIdWithLocalData_IsRejected()
    {
        var phone = CreatePhone(syncId: Guid.NewGuid());
        phone.Repository.UpdateAccount(TestData.Account(Guid.NewGuid(), "Локальное"));

        var otherSyncId = Guid.NewGuid();
        var payload = new PeerPairingPayload
        {
            SyncId = otherSyncId,
            Host = "127.0.0.1",
            Port = 12345,
            Fingerprint = "fingerprint",
            Token = "token",
            DeviceId = "desktop",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
        }.ToJson();

        var viewModel = new PeerSyncClientViewModel(
            phone.Configuration,
            phone.Repository,
            phone.LocalState,
            new FakeScanner(payload),
            new FakePermission(true));

        await viewModel.StartAsync();

        Assert.Contains("другой базы", viewModel.Status);
        Assert.NotEqual(otherSyncId, phone.Database.SyncId);
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

    private ClientNode CreatePhone(Guid syncId)
    {
        var home = Path.Combine(_root, "phone-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(home);
        var database = new DatabaseConfiguration
        {
            SyncId = syncId,
            Name = "phone",
            Path = Path.Combine(home, "database.db"),
            BackupsFolder = "Backups",
        };
        var configuration = new FakeConfiguration(database);
        var repository = new LiteDbRepository(
            configuration,
            new AccountLocalSettingsStore(configuration),
            NullLogger<LiteDbRepository>.Instance);
        repository.UpdateDbSchema();
        var localState = new LocalSyncStateStore(home);
        return new ClientNode(database, configuration, repository, localState);
    }

    private ServerNode CreateServer(Guid syncId)
    {
        var home = Path.Combine(_root, "desktop");
        Directory.CreateDirectory(home);
        var database = new DatabaseConfiguration
        {
            SyncId = syncId,
            Name = "desktop",
            Path = Path.Combine(home, "database.db"),
            BackupsFolder = "Backups",
        };
        var configuration = new FakeConfiguration(database);
        var repository = new LiteDbRepository(
            configuration,
            new AccountLocalSettingsStore(configuration),
            NullLogger<LiteDbRepository>.Instance);
        repository.UpdateDbSchema();
        var engine = new PeerSyncServerEngine(
            syncId,
            "desktop",
            "Ноутбук",
            Token,
            DateTimeOffset.UtcNow.AddMinutes(10),
            repository,
            new PeerSyncStateStore(home));
        return new ServerNode(repository, engine);
    }

    private sealed record ClientNode(
        DatabaseConfiguration Database,
        FakeConfiguration Configuration,
        LiteDbRepository Repository,
        LocalSyncStateStore LocalState);

    private sealed record ServerNode(
        LiteDbRepository Repository,
        PeerSyncServerEngine Engine);

    private sealed class FakeScanner : IQrScannerService
    {
        private readonly string? _json;

        public FakeScanner(string? json)
        {
            _json = json;
        }

        public Task<string?> ScanAsync() => Task.FromResult(_json);
    }

    private sealed class FakePermission : ILocalNetworkPermissionService
    {
        private readonly bool _granted;

        public FakePermission(bool granted)
        {
            _granted = granted;
        }

        public Task<bool> EnsureGrantedAsync() => Task.FromResult(_granted);
    }
}
