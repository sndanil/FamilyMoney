using FamilyMoney.Configuration;
using FamilyMoney.Models;
using FamilyMoney.Models.Sync;
using FamilyMoney.State;
using FamilyMoney.Sync;
using LiteDB;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace FamilyMoney.DataAccess;

public class LiteDbRepository : IRepository
{
    private const string SyncOutboxCollectionName = "_SyncOutbox";
    private const string SyncImageOutboxCollectionName = "_SyncImageOutbox";
    private const string SyncImageMetaCollectionName = "_SyncImageMeta";

    private readonly IGlobalConfiguration _configuration;
    private readonly IAccountLocalSettingsStore _accountLocalSettings;
    private readonly ILogger<LiteDbRepository> _logger;

    public LiteDbRepository(
        IGlobalConfiguration configuration,
        IAccountLocalSettingsStore accountLocalSettings,
        ILogger<LiteDbRepository> logger)
    {
        _configuration = configuration;
        _accountLocalSettings = accountLocalSettings;
        _logger = logger;
    }

    private DatabaseConfiguration DatabaseConfiguration => _configuration.GetSelectedDatabase();

    private string DatabasePath => DatabaseConfiguration.GetResolvedPath();

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

        RecalculateAccountBalances(db);

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

        MigrateAccountLocalFlags(db);

        _logger.LogInformation("End update schema");
    }

    /// <summary>
    /// Одноразовый перенос IsHidden/IsExpanded из документов Account в локальные
    /// настройки устройства: раньше флаги хранились в базе и синхронизировались.
    /// </summary>
    private void MigrateAccountLocalFlags(LiteDatabase db)
    {
        if (_accountLocalSettings.IsSeeded)
        {
            return;
        }

        var flags = db.GetCollection(nameof(Account)).FindAll()
            .Select(doc => new AccountLocalFlags(
                doc["_id"].AsGuid,
                doc.TryGetValue("IsHidden", out var hidden) && hidden.AsBoolean,
                doc.TryGetValue("IsExpanded", out var expanded) && expanded.AsBoolean))
            .Where(f => f.IsHidden || f.IsExpanded)
            .ToList();

        _accountLocalSettings.Seed(flags);
        _logger.LogInformation("Migrated local account flags for {Count} accounts", flags.Count);
    }

    public void DoBackup()
    {
        foreach (var configuration in _configuration.Get().Databases)
        {
            if (configuration != null)
            {
                DoBackup(configuration);
            }
        }
    }

    public void DoBackup(DatabaseConfiguration configuration)
    {
        try
        {
            CreateBackup(configuration, force: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backup failed for database {Database}", configuration.Name);
        }
    }

    public DatabaseBackupResult BackupNow(DatabaseConfiguration configuration)
    {
        if (configuration.MaxBackups <= 0)
        {
            return new DatabaseBackupResult(false, "Резервные копии выключены: максимум равен 0.");
        }

        if (!File.Exists(configuration.GetResolvedPath()))
        {
            return new DatabaseBackupResult(false, "Файл базы не найден.");
        }

        try
        {
            var backupPath = CreateBackup(configuration, force: true);
            if (backupPath == null)
            {
                return new DatabaseBackupResult(false, "Не удалось создать копию.");
            }

            return new DatabaseBackupResult(true, "Создана копия " + Path.GetFileName(backupPath) + ".");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backup failed for database {Database}", configuration.Name);
            return new DatabaseBackupResult(false, "Не удалось создать копию: " + ex.Message);
        }
    }

    public DatabaseBackupResult BackupToFile(DatabaseConfiguration configuration, string destinationPath)
    {
        if (configuration.MaxBackups <= 0)
        {
            return new DatabaseBackupResult(false, "Резервные копии выключены: максимум равен 0.");
        }

        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return new DatabaseBackupResult(false, "Не выбран файл резервной копии.");
        }

        var databasePath = configuration.GetResolvedPath();
        if (!File.Exists(databasePath))
        {
            return new DatabaseBackupResult(false, "Файл базы не найден.");
        }

        var destination = Path.GetFullPath(destinationPath);
        if (string.IsNullOrEmpty(Path.GetExtension(destination)))
        {
            destination += ".db";
        }

        if (PathsEqual(destination, Path.GetFullPath(databasePath)))
        {
            return new DatabaseBackupResult(false, "Нельзя сохранить копию поверх файла базы.");
        }

        try
        {
            var destinationDirectory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            using (var db = new LiteDatabase(databasePath))
            {
                db.Checkpoint();
            }

            File.Copy(databasePath, destination, overwrite: true);

            var managedDirectory = Path.GetFullPath(configuration.GetResolvedBackupDirectory());
            if (!string.IsNullOrEmpty(destinationDirectory)
                && PathsEqual(Path.GetFullPath(destinationDirectory), managedDirectory))
            {
                var others = ListBackupFiles(managedDirectory)
                    .Where(file => !PathsEqual(file.FullName, destination))
                    .Skip(Math.Max(0, configuration.MaxBackups - 1));
                foreach (var file in others)
                {
                    file.Delete();
                }
            }

            _logger.LogInformation("Backup created at {Path}", destination);
            return new DatabaseBackupResult(true, "Создана копия " + Path.GetFileName(destination) + ".");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backup failed for database {Database}", configuration.Name);
            return new DatabaseBackupResult(false, "Не удалось создать копию: " + ex.Message);
        }
    }

    public DatabaseBackupResult RestoreFromFile(DatabaseConfiguration configuration, string backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
        {
            return new DatabaseBackupResult(false, "Файл резервной копии не найден.");
        }

        var source = Path.GetFullPath(backupPath);
        var databasePath = Path.GetFullPath(configuration.GetResolvedPath());
        if (PathsEqual(source, databasePath))
        {
            return new DatabaseBackupResult(false, "Выбран файл текущей базы, а не резервная копия.");
        }

        try
        {
            EnsureDatabaseFile(source);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backup file {Path} cannot be opened", source);
            return new DatabaseBackupResult(false, "Выбранный файл не является базой данных.");
        }

        try
        {
            var directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string? safetyName = null;
            if (File.Exists(databasePath))
            {
                using (var db = new LiteDatabase(databasePath))
                {
                    db.Checkpoint();
                }

                safetyName = Path.GetFileName(SaveSafetyCopy(configuration, databasePath, source));
            }

            InstallDatabaseFile(source, databasePath);
            _logger.LogInformation("Database {Database} restored from {Path}", configuration.Name, source);
            var message = safetyName == null
                ? "База восстановлена из " + Path.GetFileName(source) + "."
                : "База восстановлена из " + Path.GetFileName(source) + ". Прежнее состояние сохранено в " + safetyName + ".";
            return new DatabaseBackupResult(true, message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Restore failed for database {Database}", configuration.Name);
            return new DatabaseBackupResult(false, "Не удалось восстановить базу: " + ex.Message);
        }
    }

    /// <summary>
    /// Снимок одной базы рядом с её файлом. Без force повторно не копирует файл с тем же содержимым.
    /// При MaxBackups не больше нуля ничего не делает. Возвращает путь созданного файла.
    /// </summary>
    private string? CreateBackup(DatabaseConfiguration configuration, bool force)
    {
        if (configuration.MaxBackups <= 0)
        {
            return null;
        }

        var databasePath = configuration.GetResolvedPath();
        if (!File.Exists(databasePath))
        {
            _logger.LogInformation("Backup skipped, database file is missing: {Path}", databasePath);
            return null;
        }

        var backupDirectory = configuration.GetResolvedBackupDirectory();
        Directory.CreateDirectory(backupDirectory);

        var source = new FileInfo(databasePath);
        var newest = ListBackupFiles(backupDirectory).FirstOrDefault();
        if (!force && newest != null && HasSameContent(source, newest))
        {
            _logger.LogInformation("Backup skipped, database {Database} is unchanged", configuration.Name);
            return null;
        }

        using (var db = new LiteDatabase(databasePath))
        {
            db.Checkpoint();
        }

        source.Refresh();
        var backupPath = Path.Combine(backupDirectory, configuration.GetSuggestedBackupFileName(DateTime.Now));
        if (File.Exists(backupPath))
        {
            backupPath = Path.Combine(
                backupDirectory,
                DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + ".db");
        }

        File.Copy(databasePath, backupPath, overwrite: false);

        foreach (var file in ListBackupFiles(backupDirectory).Skip(configuration.MaxBackups))
        {
            file.Delete();
        }

        _logger.LogInformation("Backup created at {Path}", backupPath);
        return backupPath;
    }

    private static List<FileInfo> ListBackupFiles(string backupDirectory)
    {
        return Directory.EnumerateFiles(backupDirectory, "*.db")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ThenByDescending(file => file.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Одинаковое время и размер — быстрый путь после копирования с сохранением даты.
    /// Если время сдвинулось, а размер тот же, сравнивается хеш: открытие базы
    /// не должно создавать новую копию, а правка того же размера должна.
    /// </summary>
    private static bool HasSameContent(FileInfo source, FileInfo backup)
    {
        if (source.Length != backup.Length)
        {
            return false;
        }

        if (source.LastWriteTimeUtc == backup.LastWriteTimeUtc)
        {
            return true;
        }

        return HashFile(source.FullName).AsSpan().SequenceEqual(HashFile(backup.FullName));
    }

    private static byte[] HashFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return SHA256.HashData(stream);
    }

    private string SaveSafetyCopy(DatabaseConfiguration configuration, string databasePath, string sourcePath)
    {
        var backupDirectory = configuration.GetResolvedBackupDirectory();
        Directory.CreateDirectory(backupDirectory);
        var safetyPath = Path.Combine(backupDirectory, configuration.GetSuggestedBackupFileName(DateTime.Now));
        var attempt = 0;
        while (File.Exists(safetyPath) || PathsEqual(safetyPath, sourcePath) || PathsEqual(safetyPath, databasePath))
        {
            attempt++;
            safetyPath = Path.Combine(
                backupDirectory,
                DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + "_" + attempt.ToString(CultureInfo.InvariantCulture) + ".db");
        }

        File.Copy(databasePath, safetyPath, overwrite: false);
        return safetyPath;
    }

    private static void EnsureDatabaseFile(string path)
    {
        using var db = new LiteDatabase(new ConnectionString
        {
            Filename = path,
            ReadOnly = true,
        });
        _ = db.GetCollectionNames().ToList();
    }

    private static void InstallDatabaseFile(string sourcePath, string databasePath)
    {
        var temp = databasePath + ".restore";
        File.Copy(sourcePath, temp, overwrite: true);
        try
        {
            if (!File.Exists(databasePath))
            {
                File.Move(temp, databasePath);
                return;
            }

            File.Replace(temp, databasePath, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        catch
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }

            throw;
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(
            left.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            right.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            comparison);
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

    public void RecalculateAccountBalances()
    {
        using var db = OpenDatabase();
        RecalculateAccountBalances(db);
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

    public IEnumerable<SubCategoryCommentSuggestions> GetCommentSuggestionsBySubCategories(DateTime from)
    {
        using var db = OpenDatabase();
        var collection = db.GetCollection<Transaction>(nameof(Transaction));

        var rows = collection.Query()
            .Where(t => t.Date >= from
                && t.DeletedAt == null
                && !string.IsNullOrEmpty(t.Comment)
                && t.CategoryId != null
                && t.SubCategoryId != null)
            .Select(t => new { t.CategoryId, t.SubCategoryId, t.Comment, t.Date, t.LastChange })
            .ToList();

        return rows
            .GroupBy(t => (CategoryId: t.CategoryId!.Value, SubCategoryId: t.SubCategoryId!.Value))
            .Select(g => new SubCategoryCommentSuggestions(
                g.Key.CategoryId,
                g.Key.SubCategoryId,
                CommentSuggestionBuilder.Build(g.Select(t =>
                    new CommentCandidate(t.Date, t.LastChange, t.Comment!))).ToList()))
            .ToList();
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

    private void RecalculateAccountBalances(LiteDatabase db)
    {
        var accounts = db.GetCollection<Account>(nameof(Account)).Find(a => a.DeletedAt == null).ToList();
        var transactions = db.GetCollection<Transaction>(nameof(Transaction)).Find(t => t.DeletedAt == null).ToList();
        var sums = AccountBalanceCalculator.Calculate(accounts, transactions);
        var collection = db.GetCollection<Account>(nameof(Account));

        foreach (var account in accounts)
        {
            if (account.IsGroup)
            {
                if (account.Sum != 0m)
                {
                    account.Sum = 0m;
                    collection.Update(account);
                }

                continue;
            }

            if (!sums.TryGetValue(account.Id, out var sum))
            {
                continue;
            }

            if (account.Sum != sum)
            {
                account.Sum = sum;
                collection.Update(account);
            }
        }
    }

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
