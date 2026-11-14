using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Models;
using FamilyMoney.State;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FamilyMoney.Tests;

public sealed class SyncOutboxTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "FamilyMoneyOutboxTests",
        Guid.NewGuid().ToString("N"));

    public SyncOutboxTests()
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
    public void ClearPendingSyncChanges_KeepsEditsMadeAfterThePublishedSnapshot()
    {
        var database = new DatabaseConfiguration
        {
            SyncId = Guid.NewGuid(),
            Name = "Outbox",
            Path = Path.Combine(_root, "outbox.db"),
            BackupsFolder = "Backups",
            MaxBackups = 10,
        };
        var configuration = new TestConfiguration(database);
        var repository = new LiteDbRepository(
            configuration,
            new AccountLocalSettingsStore(configuration),
            NullLogger<LiteDbRepository>.Instance);
        repository.UpdateDbSchema();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Первое имя",
        };
        repository.UpdateAccount(account);

        var published = repository.GetPendingSyncChanges();
        Assert.Single(published.Entities);

        Thread.Sleep(15);
        account.Name = "Второе имя";
        repository.UpdateAccount(account);

        repository.ClearPendingSyncChanges(published);

        var remaining = repository.GetPendingSyncChanges();
        var entry = Assert.Single(remaining.Entities);
        Assert.Contains("Второе имя", entry.DataJson);

        repository.ClearPendingSyncChanges(remaining);
        Assert.Empty(repository.GetPendingSyncChanges().Entities);
    }

    private sealed class TestConfiguration : IGlobalConfiguration
    {
        public TestConfiguration(DatabaseConfiguration database)
        {
            Root = new RootConfiguration
            {
                Databases = [database],
                SelectedDatabaseIndex = 0,
            };
        }

        public RootConfiguration Root { get; private set; }

        public RootConfiguration Get() => Root;

        public DatabaseConfiguration GetSelectedDatabase() =>
            Root.Databases[Root.SelectedDatabaseIndex];

        public void Save(RootConfiguration configuration) => Root = configuration;
    }
}
