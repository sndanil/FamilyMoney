using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Models;
using FamilyMoney.State;
using LiteDB;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FamilyMoney.Tests;

public sealed class MergeSubCategoriesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "FamilyMoneyMergeTests", Guid.NewGuid().ToString("N"));

    public MergeSubCategoriesTests()
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
    public void Merge_RewritesTransactionsAndSoftDeletesSources()
    {
        var repository = Repository();
        var categoryId = Guid.NewGuid();
        var otherCategoryId = Guid.NewGuid();
        repository.UpdateCategroty(new CreditCategory { Id = categoryId, Name = "Еда" });
        repository.UpdateCategroty(new CreditCategory { Id = otherCategoryId, Name = "Дом" });

        var target = SubCategory(categoryId, "Продукты");
        var sourceA = SubCategory(categoryId, "Магазин");
        var sourceB = SubCategory(categoryId, "Рынок");
        var other = SubCategory(otherCategoryId, "Продукты");
        repository.UpdateSubCategory(target);
        repository.UpdateSubCategory(sourceA);
        repository.UpdateSubCategory(sourceB);
        repository.UpdateSubCategory(other);

        var onTarget = Transaction(categoryId, target.Id);
        var onA = Transaction(categoryId, sourceA.Id);
        var onB = Transaction(otherCategoryId, sourceB.Id);
        var onOther = Transaction(otherCategoryId, other.Id);
        repository.InsertTransactions([onTarget, onA, onB, onOther]);

        var deletedId = Guid.NewGuid();
        var deletedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        using (var db = new LiteDatabase(DatabasePath()))
        {
            db.GetCollection<Transaction>(nameof(Transaction)).Insert(new CreditTransaction
            {
                Id = deletedId,
                CategoryId = categoryId,
                SubCategoryId = sourceA.Id,
                Sum = 5,
                Date = new DateTime(2026, 1, 1),
                LastChange = deletedAt,
                DeletedAt = deletedAt,
            });
        }

        var unknownId = Guid.NewGuid();
        var counts = repository.CountActiveTransactionsBySubCategories([target.Id, sourceA.Id, sourceB.Id, unknownId]);
        Assert.Equal(1, counts[target.Id]);
        Assert.Equal(1, counts[sourceA.Id]);
        Assert.Equal(1, counts[sourceB.Id]);
        Assert.Equal(0, counts[unknownId]);
        Assert.Empty(repository.CountActiveTransactionsBySubCategories([]));

        repository.MergeSubCategories(target.Id, [sourceA.Id, sourceB.Id, sourceA.Id]);

        Assert.Equal(target.Id, repository.GetTransaction(onTarget.Id)!.SubCategoryId);
        Assert.Equal(target.Id, repository.GetTransaction(onA.Id)!.SubCategoryId);
        Assert.Equal(categoryId, repository.GetTransaction(onA.Id)!.CategoryId);
        var movedFromOtherCategory = repository.GetTransaction(onB.Id)!;
        Assert.Equal(target.Id, movedFromOtherCategory.SubCategoryId);
        Assert.Equal(otherCategoryId, movedFromOtherCategory.CategoryId);
        Assert.Equal(other.Id, repository.GetTransaction(onOther.Id)!.SubCategoryId);

        var deleted = ReadTransaction(deletedId);
        Assert.Equal(target.Id, deleted.SubCategoryId);
        Assert.NotNull(deleted.DeletedAt);
        Assert.Equal(deletedAt, deleted.DeletedAt.Value.ToUniversalTime());

        var savedTarget = repository.GetSubCategory(target.Id);
        Assert.Equal("Продукты", savedTarget.Name);
        Assert.Null(savedTarget.DeletedAt);
        Assert.NotNull(repository.GetSubCategory(sourceA.Id).DeletedAt);
        Assert.NotNull(repository.GetSubCategory(sourceB.Id).DeletedAt);
        Assert.Null(repository.GetSubCategory(other.Id).DeletedAt);
        Assert.DoesNotContain(repository.GetSubCategories(), s => s.Id == sourceA.Id || s.Id == sourceB.Id);

        var pending = repository.GetPendingSyncChanges().Entities;
        Assert.Contains(pending, entry => entry.EntityId == sourceA.Id && entry.DeletedAt != null);
        Assert.Contains(pending, entry => entry.EntityId == sourceB.Id && entry.DeletedAt != null);
        var moved = Assert.Single(pending, entry => entry.EntityId == onA.Id);
        Assert.Null(moved.DeletedAt);
        Assert.Contains(target.Id.ToString(), moved.DataJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(pending, entry => entry.EntityId == deletedId && entry.DeletedAt != null);
    }

    [Fact]
    public void Merge_DifferentCategories_LeavesDataUntouched()
    {
        var repository = Repository();
        var foodId = Guid.NewGuid();
        var homeId = Guid.NewGuid();
        var target = SubCategory(foodId, "Продукты");
        var source = SubCategory(homeId, "Ремонт");
        repository.UpdateSubCategory(target);
        repository.UpdateSubCategory(source);
        var transaction = Transaction(homeId, source.Id);
        repository.InsertTransactions([transaction]);

        Assert.Throws<InvalidOperationException>(() => repository.MergeSubCategories(target.Id, [source.Id]));

        Assert.Equal(source.Id, repository.GetTransaction(transaction.Id)!.SubCategoryId);
        Assert.Null(repository.GetSubCategory(target.Id).DeletedAt);
        Assert.Null(repository.GetSubCategory(source.Id).DeletedAt);
    }

    [Fact]
    public void Merge_DifferentTypes_LeavesDataUntouched()
    {
        var repository = Repository();
        var categoryId = Guid.NewGuid();
        var target = SubCategory(categoryId, "Продукты");
        var source = new DebetSubCategory
        {
            Id = Guid.NewGuid(),
            CategoryId = categoryId,
            Name = "Зарплата",
        };
        repository.UpdateSubCategory(target);
        repository.UpdateSubCategory(source);

        Assert.Throws<InvalidOperationException>(() => repository.MergeSubCategories(target.Id, [source.Id]));

        Assert.Null(repository.GetSubCategory(target.Id).DeletedAt);
        Assert.Null(repository.GetSubCategory(source.Id).DeletedAt);
    }

    [Fact]
    public void Merge_EmptySources_Throws()
    {
        var repository = Repository();

        Assert.Throws<InvalidOperationException>(() => repository.MergeSubCategories(Guid.NewGuid(), []));
        Assert.Throws<ArgumentNullException>(() => repository.MergeSubCategories(Guid.NewGuid(), null!));
    }

    [Fact]
    public void Merge_TargetAmongSources_LeavesDataUntouched()
    {
        var repository = Repository();
        var categoryId = Guid.NewGuid();
        var target = SubCategory(categoryId, "Продукты");
        var source = SubCategory(categoryId, "Магазин");
        repository.UpdateSubCategory(target);
        repository.UpdateSubCategory(source);
        var transaction = Transaction(categoryId, source.Id);
        repository.InsertTransactions([transaction]);

        Assert.Throws<InvalidOperationException>(() => repository.MergeSubCategories(target.Id, [target.Id, source.Id]));

        Assert.Equal(source.Id, repository.GetTransaction(transaction.Id)!.SubCategoryId);
        Assert.Null(repository.GetSubCategory(target.Id).DeletedAt);
        Assert.Null(repository.GetSubCategory(source.Id).DeletedAt);
    }

    private string DatabasePath() => Path.Combine(_root, "family.db");

    private LiteDbRepository Repository()
    {
        var configuration = new DatabaseConfiguration
        {
            Name = "Тест",
            Path = DatabasePath(),
            SyncId = Guid.NewGuid(),
            BackupsFolder = "Backups",
            MaxBackups = 1,
        };

        return new LiteDbRepository(
            new FixedConfiguration(configuration),
            new UnusedAccountSettings(),
            NullLogger<LiteDbRepository>.Instance);
    }

    private static CreditSubCategory SubCategory(Guid categoryId, string name)
    {
        return new CreditSubCategory
        {
            Id = Guid.NewGuid(),
            CategoryId = categoryId,
            Name = name,
        };
    }

    private static CreditTransaction Transaction(Guid categoryId, Guid subCategoryId)
    {
        return new CreditTransaction
        {
            Id = Guid.NewGuid(),
            CategoryId = categoryId,
            SubCategoryId = subCategoryId,
            Sum = 10,
            Date = new DateTime(2026, 3, 1),
        };
    }

    private Transaction ReadTransaction(Guid id)
    {
        using var db = new LiteDatabase(DatabasePath());
        return db.GetCollection<Transaction>(nameof(Transaction)).FindOne(t => t.Id == id);
    }

    private sealed class FixedConfiguration(DatabaseConfiguration database) : IGlobalConfiguration
    {
        private RootConfiguration _root = new()
        {
            Databases = [database],
        };

        public RootConfiguration Get() => _root;

        public DatabaseConfiguration GetSelectedDatabase() => _root.Databases[0];

        public void Save(RootConfiguration configuration) => _root = configuration;
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

        public void Seed(IEnumerable<AccountLocalFlags> flags)
        {
        }
    }
}
