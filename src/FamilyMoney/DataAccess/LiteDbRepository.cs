using FamilyMoney.Configuration;
using FamilyMoney.Models;
using FamilyMoney.Models.Sync;
using FamilyMoney.Sync;
using LiteDB;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace FamilyMoney.DataAccess;

public class LiteDbRepository : IRepository
{
    private const string SyncOutboxCollectionName = "_SyncOutbox";
    private const string SyncImageOutboxCollectionName = "_SyncImageOutbox";
    private const string SyncImageMetaCollectionName = "_SyncImageMeta";

    private readonly IGlobalConfiguration _configuration;
    private readonly ILogger<LiteDbRepository> _logger;

    public LiteDbRepository(IGlobalConfiguration configuration, ILogger<LiteDbRepository> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private DatabaseConfiguration DatabaseConfiguration => _configuration.GetSelectedDatabase();

    private string DatabasePath => DatabaseConfiguration.GetResolvedPath();

    private string BackupsFolder => DatabaseConfiguration.GetResolvedBackupsFolder();

    public SyncPendingChanges GetPendingSyncChanges()
    {
        using var db = OpenDatabase();
        return new SyncPendingChanges
        {
            Entities = db.GetCollection<SyncOutboxEntry>(SyncOutboxCollectionName).FindAll().ToList(),
            Images = db.GetCollection<SyncImageOutboxEntry>(SyncImageOutboxCollectionName).FindAll().ToList(),
        };
    }

    public void ClearPendingSyncChanges()
    {
        using var db = OpenDatabase();
        db.GetCollection<SyncOutboxEntry>(SyncOutboxCollectionName).DeleteAll();
        db.GetCollection<SyncImageOutboxEntry>(SyncImageOutboxCollectionName).DeleteAll();
    }

    public void ApplySyncDelta(SyncDelta delta)
    {
        using var _ = SyncContext.EnterApplyScope();
        using var db = OpenDatabase();

        foreach (var record in delta.Accounts)
        {
            ApplySyncRecord(db.GetCollection<Account>(nameof(Account)), record);
        }

        foreach (var record in delta.Categories)
        {
            ApplySyncRecord(db.GetCollection<Category>(nameof(Category)), record);
        }

        foreach (var record in delta.SubCategories)
        {
            ApplySyncRecord(db.GetCollection<SubCategory>(nameof(SubCategory)), record);
        }

        foreach (var record in delta.Transactions)
        {
            ApplySyncRecord(db.GetCollection<Transaction>(nameof(Transaction)), record);
        }

        _logger.LogInformation(
            "Applied sync delta {Revision} from device {DeviceId}",
            delta.Revision,
            delta.DeviceId);
    }

    public async Task ApplySyncedImagesAsync(
        IEnumerable<SyncImageRecord> images,
        Func<SyncImageRecord, CancellationToken, Task<Stream?>> downloadImageAsync,
        CancellationToken cancellationToken = default)
    {
        using var _ = SyncContext.EnterApplyScope();
        using var db = OpenDatabase();
        var metaCollection = db.GetCollection<SyncImageMeta>(SyncImageMetaCollectionName);

        foreach (var image in images)
        {
            var localMeta = metaCollection.FindOne(x => x.EntityId == image.EntityId);
            if (localMeta != null && !ShouldReplaceImage(localMeta, image))
            {
                continue;
            }

            var storageId = image.EntityId.ToString();
            if (image.DeletedAt != null)
            {
                db.FileStorage.Delete(storageId);
                metaCollection.Upsert(new SyncImageMeta
                {
                    EntityId = image.EntityId,
                    LastChange = image.LastChange,
                    DeletedAt = image.DeletedAt,
                    FileName = image.FileName ?? "image",
                });
                continue;
            }

            await using var stream = await downloadImageAsync(image, cancellationToken);
            if (stream == null)
            {
                _logger.LogWarning("Remote image {EntityId} was not downloaded", image.EntityId);
                continue;
            }

            var fileName = string.IsNullOrWhiteSpace(image.FileName) ? "image" : image.FileName;
            db.FileStorage.Upload(storageId, fileName, stream);
            metaCollection.Upsert(new SyncImageMeta
            {
                EntityId = image.EntityId,
                LastChange = image.LastChange,
                DeletedAt = null,
                FileName = fileName,
            });
        }
    }

    public void UpdateDbSchema()
    {
        _logger.LogInformation("Start update schema");

        var directory = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var db = OpenDatabase();

        var subcategories = db.GetCollection<SubCategory>(nameof(SubCategory));
        subcategories.EnsureIndex(s => s.Name);

        var transactions = db.GetCollection<Transaction>(nameof(Transaction));
        transactions.EnsureIndex(t => t.Date);
        transactions.EnsureIndex(t => t.AccountId);
        transactions.EnsureIndex(t => t.SubCategoryId);

        _logger.LogInformation("End update schema");
    }

    public void DoBackup()
    {
        DoBackup(DatabaseConfiguration);
    }

    public void DoBackup(DatabaseConfiguration configuration)
    {
        var databasePath = configuration.GetResolvedPath();
        if (!File.Exists(databasePath))
        {
            return;
        }

        var backupsFolder = configuration.GetResolvedBackupsFolder();
        if (!Directory.Exists(backupsFolder))
        {
            Directory.CreateDirectory(backupsFolder);
        }

        var backupPath = Path.Combine(backupsFolder, "Backup_" + DateTime.Now.ToString("yyyy_MM_dd", CultureInfo.InvariantCulture) + ".db");
        if (!File.Exists(backupPath))
        {
            File.Copy(databasePath, backupPath);
        }

        foreach (var file in Directory.GetFiles(backupsFolder).OrderByDescending(b => b).Skip(configuration.MaxBackups))
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    public void UpdateImage(Guid id, string fileName, Stream stream)
    {
        var strId = id.ToString();
        using var db = OpenDatabase();
        db.FileStorage.Upload(strId, fileName, stream);
        EnqueueImageUpload(db, id, fileName);
    }

    public Stream? TryGetImage(Guid id)
    {
        var strId = id.ToString();
        using var db = OpenDatabase();
        var file = db.FileStorage.FindById(strId);
        if (file != null)
        {
            var memoryStream = new MemoryStream();
            file.CopyTo(memoryStream);
            memoryStream.Position = 0;

            return memoryStream;
        }

        return null;
    }

    public Account GetAccount(Guid id)
    {
        using var db = OpenDatabase();
        var collection = db.GetCollection<Account>(nameof(Account));
        return collection.FindOne(c => c.Id == id);
    }

    public IEnumerable<Account> GetAccounts()
    {
        using var db = OpenDatabase();
        var collection = db.GetCollection<Account>(nameof(Account));
        return collection.Find(a => a.DeletedAt == null).ToList();
    }

    public void UpdateAccount(Account account)
    {
        Touch(account);
        using var db = OpenDatabase();
        var collection = db.GetCollection<Account>(nameof(Account));
        collection.Upsert(account);
        EnqueueEntity(db, account);
    }

    public void DeleteAccount(Guid id)
    {
        using var db = OpenDatabase();
        var collection = db.GetCollection<Account>(nameof(Account));
        var account = collection.FindOne(c => c.Id == id);
        if (account == null)
        {
            return;
        }

        MarkDeleted(account);
        collection.Upsert(account);
        EnqueueEntity(db, account);
        EnqueueImageDelete(db, id);
        db.FileStorage.Delete(id.ToString());
    }

    public void UpdateCategroty(Category category)
    {
        Touch(category);
        using var db = OpenDatabase();
        var collection = db.GetCollection<Category>(nameof(Category));
        collection.Upsert(category);
        EnqueueEntity(db, category);
    }

    public Category GetCategory(Guid id)
    {
        using var db = OpenDatabase();
        var collection = db.GetCollection<Category>(nameof(Category));
        return collection.FindOne(c => c.Id == id);
    }

    public IEnumerable<Category> GetCategories()
    {
        using var db = OpenDatabase();
        var collection = db.GetCollection<Category>(nameof(Category));
        return collection.Find(c => c.DeletedAt == null).ToList();
    }

    public SubCategory GetSubCategory(Guid id)
    {
        using var db = OpenDatabase();
        var collection = db.GetCollection<SubCategory>(nameof(SubCategory));
        return collection.FindOne(c => c.Id == id);
    }

    public SubCategory GetOrCreateSubCategory(Guid? categoryId, string name, Func<SubCategory> factory)
    {
        using var db = OpenDatabase();
        var collection = db.GetCollection<SubCategory>(nameof(SubCategory));
        var result = collection.Query()
                        .Where(s => s.CategoryId == categoryId && s.Name == name && s.DeletedAt == null)
                        .FirstOrDefault();

        if (result == null)
        {
            result = collection.Query()
                            .Where(s => s.CategoryId == categoryId && s.Name.ToLower() == name.ToLower() && s.DeletedAt == null)
                            .FirstOrDefault();
        }

        if (result == null)
        {
            result = factory();
            Touch(result);
            collection.Insert(result);
            EnqueueEntity(db, result);
        }

        return result;
    }

    public IEnumerable<SubCategory> GetSubCategories()
    {
        using var db = OpenDatabase();
        var collection = db.GetCollection<SubCategory>(nameof(SubCategory));
        return collection.Find(s => s.DeletedAt == null).ToList();
    }

    public void UpdateSubCategory(SubCategory subCategory)
    {
        Touch(subCategory);
        using var db = OpenDatabase();
        var collection = db.GetCollection<SubCategory>(nameof(SubCategory));
        collection.Upsert(subCategory);
        EnqueueEntity(db, subCategory);
    }

    public IEnumerable<SubCategoryLastSum> GetLastSumsBySubCategories(DateTime from, IEnumerable<Guid> subCategoryIds)
    {
        using var db = OpenDatabase();
        var collection = db.GetCollection<Transaction>(nameof(Transaction));

        var result = new List<SubCategoryLastSum>();
        foreach (var subCategory in subCategoryIds.Distinct())
        {
            db.BeginTrans();
            var query = collection.Query()
                .Where(t => t.Date >= from && t.SubCategoryId == subCategory && t.DeletedAt == null)
                .OrderByDescending(t => t.Date)
                .Limit(1);
            var transaction = query.FirstOrDefault();
            if (transaction != null)
            {
                result.Add(new(subCategory, transaction.Sum));
            }

            db.Rollback();
        }

        return result;
    }

    public IEnumerable<SubCategoryLastComments> GetCommentsBySubCategories(DateTime from)
    {
        using var db = OpenDatabase();
        var collection = db.GetCollection<Transaction>(nameof(Transaction));

        var query = collection.Query()
            .Where(t => t.Date >= from && t.DeletedAt == null && !string.IsNullOrEmpty(t.Comment))
            .Select(t => new { t.SubCategoryId, t.Comment });

        var result = query
            .ToList()
            .Distinct()
            .GroupBy(t => t.SubCategoryId.GetValueOrDefault())
            .Select(t => new SubCategoryLastComments(t.Key, t.Where(c => !string.IsNullOrEmpty(c.Comment)).Select(c => c.Comment!).ToList()))
            .ToList();

        return result;
    }

    public IEnumerable<SubCategoryTags> GetTagsBySubCategories(DateTime from)
    {
        using var db = OpenDatabase();
        var collection = db.GetCollection<Transaction>(nameof(Transaction));

        return collection.Query()
            .Where(t => t.Date >= from && t.DeletedAt == null && t.SubCategoryId != null && t.CategoryId != null)
            .ToList()
            .Where(t => t.Tags is { Length: > 0 })
            .GroupBy(t => (CategoryId: t.CategoryId!.Value, SubCategoryId: t.SubCategoryId!.Value))
            .Select(g => new SubCategoryTags(
                g.Key.SubCategoryId,
                g.Key.CategoryId,
                g.SelectMany(t => t.Tags!)
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Select(tag => tag.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(tag => tag)
                    .ToList()))
            .ToList();
    }

    public IEnumerable<Transaction> GetTransactions(TransactionsFilter filter)
    {
        using var db = OpenDatabase();
        var collection = db.GetCollection<Transaction>(nameof(Transaction));

        var query = collection.Query()
            .Where(t => t.Date >= filter.PeriodFrom && t.Date <= filter.PeriodTo && t.DeletedAt == null);

        if (filter.AccountId.HasValue)
        {
            var accountsCollection = db.GetCollection<Account>(nameof(Account));
            var accounts = accountsCollection.Find(a => a.Id == filter.AccountId || a.ParentId == filter.AccountId);
            var queries = new List<BsonExpression>();
            foreach (var account in accounts)
            {
                queries.Add(Query.EQ(nameof(Transaction.AccountId), account.Id));
                queries.Add(Query.EQ(nameof(TransferTransaction.ToAccountId), account.Id));
            }

            query = query.Where(Query.Or(queries.ToArray()));
        }

        return query.OrderByDescending(t => t.Date)
            .ToList();
    }

    public Transaction? GetTransaction(Guid id)
    {
        using var db = OpenDatabase();
        var collection = db.GetCollection<Transaction>(nameof(Transaction));
        var transaction = collection.FindById(id);
        return transaction is { DeletedAt: not null } ? null : transaction;
    }

    public void DeleteTransaction(Guid id)
    {
        using var db = OpenDatabase();
        var collection = db.GetCollection<Transaction>(nameof(Transaction));
        var transaction = collection.FindById(id);
        if (transaction == null)
        {
            return;
        }

        MarkDeleted(transaction);
        collection.Upsert(transaction);
        EnqueueEntity(db, transaction);
    }

    public void UpdateTransaction(Transaction transaction)
    {
        if (transaction.LastChange == default)
        {
            transaction.LastChange = DateTime.UtcNow;
        }

        transaction.DeletedAt = null;
        using var db = OpenDatabase();
        var collection = db.GetCollection<Transaction>(nameof(Transaction));
        collection.Upsert(transaction);
        EnqueueEntity(db, transaction);
    }

    public void InsertTransactions(IEnumerable<Transaction> transactions)
    {
        var items = transactions.ToList();
        using var db = OpenDatabase();
        var collection = db.GetCollection<Transaction>(nameof(Transaction));
        foreach (var transaction in items)
        {
            Touch(transaction);
            collection.Insert(transaction);
            EnqueueEntity(db, transaction);
        }
    }

    private LiteDatabase OpenDatabase() => new(DatabasePath);

    private static void EnqueueEntity(LiteDatabase db, ISyncable entity)
    {
        if (SyncContext.IsApplying)
        {
            return;
        }

        var collection = db.GetCollection<SyncOutboxEntry>(SyncOutboxCollectionName);
        var record = SyncEntitySerializer.CreateRecord(entity);
        var existing = collection.FindOne(x => x.EntityType == record.EntityType && x.EntityId == record.Id);
        if (existing != null)
        {
            existing.LastChange = record.LastChange;
            existing.DeletedAt = record.DeletedAt;
            existing.DataJson = record.DataJson;
            collection.Update(existing);
            return;
        }

        collection.Insert(new SyncOutboxEntry
        {
            EntityType = record.EntityType,
            EntityId = record.Id,
            LastChange = record.LastChange,
            DeletedAt = record.DeletedAt,
            DataJson = record.DataJson,
        });
    }

    private static void EnqueueImageUpload(LiteDatabase db, Guid entityId, string fileName)
    {
        if (SyncContext.IsApplying)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var collection = db.GetCollection<SyncImageOutboxEntry>(SyncImageOutboxCollectionName);
        var existing = collection.FindOne(x => x.EntityId == entityId);
        if (existing != null)
        {
            existing.LastChange = now;
            existing.DeletedAt = null;
            existing.FileName = fileName;
            collection.Update(existing);
            return;
        }

        collection.Insert(new SyncImageOutboxEntry
        {
            EntityId = entityId,
            LastChange = now,
            FileName = fileName,
        });
    }

    private static void EnqueueImageDelete(LiteDatabase db, Guid entityId)
    {
        if (SyncContext.IsApplying)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var collection = db.GetCollection<SyncImageOutboxEntry>(SyncImageOutboxCollectionName);
        var existing = collection.FindOne(x => x.EntityId == entityId);
        if (existing != null)
        {
            existing.LastChange = now;
            existing.DeletedAt = now;
            collection.Update(existing);
            return;
        }

        collection.Insert(new SyncImageOutboxEntry
        {
            EntityId = entityId,
            LastChange = now,
            DeletedAt = now,
        });
    }

    private static bool ShouldReplaceImage(SyncImageMeta local, SyncImageRecord remote)
    {
        if (remote.LastChange > local.LastChange)
        {
            return true;
        }

        if (remote.LastChange < local.LastChange)
        {
            return false;
        }

        return remote.DeletedAt.HasValue && !local.DeletedAt.HasValue;
    }

    private static void ApplySyncRecord<T>(ILiteCollection<T> collection, SyncEntityRecord record)
        where T : class, ISyncable
    {
        var existing = collection.FindOne(x => x.Id == record.Id);
        if (record.DeletedAt != null)
        {
            if (existing == null || !ShouldReplaceSyncRecord(existing, record))
            {
                return;
            }

            existing.LastChange = record.LastChange;
            existing.DeletedAt = record.DeletedAt;
            collection.Update(existing);
            return;
        }

        var entity = SyncEntitySerializer.Deserialize(record) as T;
        if (entity == null)
        {
            return;
        }

        if (existing != null && !ShouldReplaceSyncRecord(existing, record))
        {
            return;
        }

        collection.Upsert(entity);
    }

    private static bool ShouldReplaceSyncRecord(ISyncable existing, SyncEntityRecord incoming)
    {
        if (incoming.LastChange > existing.LastChange)
        {
            return true;
        }

        if (incoming.LastChange < existing.LastChange)
        {
            return false;
        }

        return incoming.DeletedAt.HasValue && !existing.DeletedAt.HasValue;
    }

    private static void Touch(ISyncable entity)
    {
        entity.LastChange = DateTime.UtcNow;
        entity.DeletedAt = null;
    }

    private static void MarkDeleted(ISyncable entity)
    {
        entity.LastChange = DateTime.UtcNow;
        entity.DeletedAt = DateTime.UtcNow;
    }
}
