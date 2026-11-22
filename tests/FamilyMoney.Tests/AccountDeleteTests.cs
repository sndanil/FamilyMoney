using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Models;
using FamilyMoney.State;
using FamilyMoney.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace FamilyMoney.Tests;

public sealed class AccountDeleteTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"fm-account-delete-{Guid.NewGuid():N}");

    public AccountDeleteTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Временная папка — не повод валить тест.
        }
    }

    [Fact]
    public void DeleteAccount_MarksDeletedAndRemovesFromTree()
    {
        var (repository, configuration) = CreateRepository();
        var (viewModel, _) = CreateViewModel(repository, configuration);

        var (group, account) = CreateGroupedAccount(repository, viewModel);

        viewModel.DeleteAccount(account);

        Assert.DoesNotContain(repository.GetAccounts(), item => item.Id == account.Id);
        Assert.Contains(
            repository.GetAccounts(includeDeleted: true),
            item => item.Id == account.Id && item.DeletedAt != null);

        Assert.Empty(viewModel.Total.Children.Single(item => item.Id == group.Id).Children);
    }

    [Fact]
    public void DeleteAccount_ResetsSelection()
    {
        var (repository, configuration) = CreateRepository();
        var (viewModel, stateManager) = CreateViewModel(repository, configuration);

        var (_, account) = CreateGroupedAccount(repository, viewModel);

        stateManager.SetMainState(stateManager.GetMainState() with { SelectedAccountId = account.Id });

        viewModel.DeleteAccount(account);

        Assert.Null(stateManager.GetMainState().SelectedAccountId);
    }

    [Fact]
    public void DeleteAccount_KeepsOtherAccounts()
    {
        var (repository, configuration) = CreateRepository();
        var (viewModel, _) = CreateViewModel(repository, configuration);

        var (_, account) = CreateGroupedAccount(repository, viewModel);

        viewModel.DeleteAccount(account);

        Assert.Single(viewModel.Total.Children);
        Assert.NotNull(repository.GetAccounts().Single());
    }

    [Fact]
    public void DeleteBlocked_ForAccountWithMoney()
    {
        var account = new AccountViewModel { Id = Guid.NewGuid(), Name = "Карта", Sum = 100m };

        Assert.Equal("Удалить можно только счёт с нулевой суммой", AccountsViewModel.GetDeleteBlockedReason(account));
    }

    [Fact]
    public void DeleteBlocked_ForNonEmptyGroup()
    {
        var group = new AccountViewModel { Id = Guid.NewGuid(), Name = "Наличные", IsGroup = true };
        group.Children.Add(new AccountViewModel { Id = Guid.NewGuid(), Name = "Кошелёк" });

        Assert.Equal("Сначала перенесите счета из группы", AccountsViewModel.GetDeleteBlockedReason(group));
    }

    [Fact]
    public void DeleteAllowed_ForEmptyAccountWithZeroSum()
    {
        var account = new AccountViewModel { Id = Guid.NewGuid(), Name = "Кошелёк", Sum = 0m };

        Assert.Null(AccountsViewModel.GetDeleteBlockedReason(account));
    }

    [Fact]
    public void DeleteBlocked_ForNewAccount()
    {
        var account = new AccountViewModel { Name = "Новый" };

        Assert.Equal("Счёт ещё не сохранён", AccountsViewModel.GetDeleteBlockedReason(account));
    }

    private (Account Group, AccountViewModel Account) CreateGroupedAccount(
        LiteDbRepository repository,
        AccountsViewModel viewModel)
    {
        var group = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Наличные",
            IsGroup = true,
            Order = 0,
        };
        repository.UpdateAccount(group);

        var leaf = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Кошелёк",
            ParentId = group.Id,
            Order = 0,
        };
        repository.UpdateAccount(leaf);

        viewModel.LoadAccounts();

        var loaded = viewModel.Total.Children
            .Single(item => item.Id == group.Id)
            .Children
            .Single(item => item.Id == leaf.Id);

        return (group, loaded);
    }

    private (AccountsViewModel ViewModel, FakeStateManager State) CreateViewModel(
        LiteDbRepository repository,
        IGlobalConfiguration configuration)
    {
        var state = new MainState([], null, DateTime.Today, DateTime.Today);
        var stateManager = new FakeStateManager(state);
        var viewModel = new AccountsViewModel(repository, stateManager, new UnusedAccountSettings(), configuration);

        return (viewModel, stateManager);
    }

    private (LiteDbRepository Repository, IGlobalConfiguration Configuration) CreateRepository()
    {
        var database = new DatabaseConfiguration
        {
            SyncId = Guid.NewGuid(),
            Name = "Accounts",
            Path = Path.Combine(_root, "accounts.db"),
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

        public DatabaseConfiguration GetSelectedDatabase() => _root.Databases[_root.SelectedDatabaseIndex];

        public void Save(RootConfiguration configuration)
        {
        }
    }

    private sealed class UnusedAccountSettings : IAccountLocalSettingsStore
    {
        public bool IsSeeded => true;

        public bool IsHidden(Guid accountId) => false;

        public bool IsExpanded(Guid accountId) => false;

        public void SetHidden(Guid accountId, bool value)
        {
        }

        public void SetExpanded(Guid accountId, bool value)
        {
        }

        public IReadOnlyCollection<Guid> GetHiddenAccountIds() => [];

        public void ApplyPeerHidden(IEnumerable<Guid> accountIds)
        {
        }

        public void Seed(IEnumerable<AccountLocalFlags> flags)
        {
        }
    }
}
