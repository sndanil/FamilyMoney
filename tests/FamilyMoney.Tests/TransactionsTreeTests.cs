using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using FamilyMoney.Models;
using FamilyMoney.State;
using FamilyMoney.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FamilyMoney.Tests;

/// <summary>
/// Построение дерева транзакций не должно падать из-за ссылок на удалённые
/// или несинхронизированные категории и подкатегории.
/// </summary>
public sealed class TransactionsTreeTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "FamilyMoneyTreeTests",
        Guid.NewGuid().ToString("N"));

    public TransactionsTreeTests()
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
    public async Task MissingSubCategory_DoesNotThrow_AndFallsBackToNoSubCategory()
    {
        var (repository, configuration) = CreateRepository();
        var category = new CreditCategory { Id = Guid.NewGuid(), Name = "Продукты" };
        repository.UpdateCategroty(category);

        repository.UpdateTransaction(new CreditTransaction
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            SubCategoryId = Guid.NewGuid(),
            Sum = 120m,
            Date = DateTime.Today,
            LastChange = DateTime.UtcNow,
        });

        var viewModel = await BuildAsync(repository, configuration);

        var categoryGroup = Assert.Single(viewModel.CreditTransactions.Categories);
        Assert.Equal("Продукты", categoryGroup.DisplayName);

        var subCategoryGroup = Assert.Single(categoryGroup.SubCategories);
        Assert.Null(subCategoryGroup.SubCategory);
        Assert.Single(subCategoryGroup.Transactions);

        WeakReferenceMessenger.Default.UnregisterAll(viewModel);
    }

    [Fact]
    public async Task MissingCategory_DoesNotThrow_AndFallsBackToNoCategory()
    {
        var (repository, configuration) = CreateRepository();

        repository.UpdateTransaction(new CreditTransaction
        {
            Id = Guid.NewGuid(),
            CategoryId = Guid.NewGuid(),
            SubCategoryId = null,
            Sum = 50m,
            Date = DateTime.Today,
            LastChange = DateTime.UtcNow,
        });

        var viewModel = await BuildAsync(repository, configuration);

        var categoryGroup = Assert.Single(viewModel.CreditTransactions.Categories);
        Assert.Equal("Без категории", categoryGroup.DisplayName);
        Assert.Null(categoryGroup.Category);

        WeakReferenceMessenger.Default.UnregisterAll(viewModel);
    }

    private async Task<TransactionsViewModel> BuildAsync(IRepository repository, IGlobalConfiguration configuration)
    {
        var state = new MainState([], null, DateTime.Today, DateTime.Today);
        var viewModel = new TransactionsViewModel(repository, new FakeStateManager(state), configuration);
        await viewModel.RefreshAsync(state);
        return viewModel;
    }

    private (LiteDbRepository Repository, IGlobalConfiguration Configuration) CreateRepository()
    {
        var database = new DatabaseConfiguration
        {
            SyncId = Guid.NewGuid(),
            Name = "Tree",
            Path = Path.Combine(_root, "tree.db"),
            BackupsFolder = "Backups",
            MaxBackups = 10,
        };

        var configuration = new FakeConfiguration(database);
        var repository = new LiteDbRepository(
            configuration,
            new AccountLocalSettingsStore(configuration),
            NullLogger<LiteDbRepository>.Instance);

        repository.UpdateDbSchema();
        return (repository, configuration);
    }

    private sealed class FakeStateManager : IStateManager
    {
        private MainState _state;

        public FakeStateManager(MainState state)
        {
            _state = state;
        }

        public MainState GetMainState() => _state;

        public void SetMainState(MainState state) => _state = state;
    }

    private sealed class FakeConfiguration : IGlobalConfiguration
    {
        private readonly RootConfiguration _root;

        public FakeConfiguration(DatabaseConfiguration database)
        {
            _root = new RootConfiguration
            {
                Databases = [database],
                SelectedDatabaseIndex = 0,
            };
        }

        public RootConfiguration Get() => _root;

        public DatabaseConfiguration GetSelectedDatabase() => _root.Databases[0];

        public void Save(RootConfiguration configuration)
        {
        }
    }
}
