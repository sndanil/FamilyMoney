using FamilyMoney.Configuration;
using FamilyMoney.Models;
using LiteDB;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace FamilyMoney.DataAccess;

public class LiteDbRepository : IRepository
{
    private DatabaseConfiguration _databaseConfiguration;

    public LiteDbRepository(IGlobalConfiguration configuration)
    {
        _databaseConfiguration = configuration.Get().Database;
    }

    public void UpdateDbSchema()
    {
        using var db = new LiteDatabase(_databaseConfiguration.Path);

        var subcategories = db.GetCollection<SubCategory>(nameof(SubCategory));
        subcategories.EnsureIndex(s => s.Name);

        var transactions = db.GetCollection<Transaction>(nameof(Transaction));
        transactions.EnsureIndex(t => t.Date);
        transactions.EnsureIndex(t => t.AccountId);
        transactions.EnsureIndex(t => t.SubCategoryId);
    }

    public void DoBackup()
    {
        if (File.Exists(_databaseConfiguration.Path))
        {
            var backupsFolder = _databaseConfiguration.BackupsFolder;
            if (!Directory.Exists(backupsFolder))
            {
                Directory.CreateDirectory(backupsFolder);
            }

            var backupPath = Path.Combine(backupsFolder, "Backup_" + DateTime.Now.ToString("yyyy_MM_dd", CultureInfo.InvariantCulture) + ".db");
            if (!File.Exists(backupPath))
            {
                File.Copy(_databaseConfiguration.Path, backupPath);
            }

            foreach (var file in Directory.GetFiles(backupsFolder).OrderByDescending(b => b).Skip(_databaseConfiguration.MaxBackups))
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }
    }

    public void UpdateImage(Guid id, string fileName, Stream stream)
    {
        var strId = id.ToString();
        using var db = new LiteDatabase(_databaseConfiguration.Path);
        db.FileStorage.Upload(strId, fileName, stream);
    }

    public Stream? TryGetImage(Guid id)
    {
        var strId = id.ToString();
        using var db = new LiteDatabase(_databaseConfiguration.Path);
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
        using var db = new LiteDatabase(_databaseConfiguration.Path);
        var collection = db.GetCollection<Account>(nameof(Account));
        return collection.FindOne(c => c.Id == id);
    }

    public IEnumerable<Account> GetAccounts()
    {
        using var db = new LiteDatabase(_databaseConfiguration.Path);
        var collection = db.GetCollection<Account>(nameof(Account));
        return collection.FindAll().ToList();
    }

    public void UpdateAccount(Account account)
    {
        using var db = new LiteDatabase(_databaseConfiguration.Path);
        var collection = db.GetCollection<Account>(nameof(Account));
        collection.Upsert(account);
    }

    public void DeleteAccount(Guid id)
    {
        using var db = new LiteDatabase(_databaseConfiguration.Path);
        var collection = db.GetCollection<Account>(nameof(Account));
        collection.Delete(id);
        db.FileStorage.Delete(id.ToString());
    }

    public void UpdateCategroty(Category category)
    {
        using var db = new LiteDatabase(_databaseConfiguration.Path);
        var collection = db.GetCollection<Category>(nameof(Category));
        collection.Upsert(category);
    }

    public Category GetCategory(Guid id)
    {
        using var db = new LiteDatabase(_databaseConfiguration.Path);
        var collection = db.GetCollection<Category>(nameof(Category));
        return collection.FindOne(c => c.Id == id);
    }

    public IEnumerable<Category> GetCategories()
    {
        using var db = new LiteDatabase(_databaseConfiguration.Path);
        var collection = db.GetCollection<Category>(nameof(Category));
        return collection.FindAll().ToList();
    }

    public SubCategory GetSubCategory(Guid id)
    {
        using var db = new LiteDatabase(_databaseConfiguration.Path);
        var collection = db.GetCollection<SubCategory>(nameof(SubCategory));
        return collection.FindOne(c => c.Id == id);
    }

    public SubCategory GetOrCreateSubCategory(Guid? categoryId, string name, Func<SubCategory> factory)
    {
        using var db = new LiteDatabase(_databaseConfiguration.Path);
        var collection = db.GetCollection<SubCategory>(nameof(SubCategory));
        var result = collection.Query()
                        .Where(s => s.CategoryId == categoryId && s.Name == name)
                        .FirstOrDefault();

        if (result == null)
        {
            result = collection.Query()
                            .Where(s => s.CategoryId == categoryId && s.Name.ToLower() == name.ToLower())
                            .FirstOrDefault();
        }
        if (result == null)
        {
            result = factory();
            collection.Insert(result);
        }

        return result;
    }

    public IEnumerable<SubCategory> GetSubCategories()
    {
        using var db = new LiteDatabase(_databaseConfiguration.Path);
        var collection = db.GetCollection<SubCategory>(nameof(SubCategory));
        return collection.FindAll().ToList();
    }

    public void UpdateSubCategory(SubCategory subCategory)
    {
        using var db = new LiteDatabase(_databaseConfiguration.Path);
        var collection = db.GetCollection<SubCategory>(nameof(SubCategory));
        collection.Upsert(subCategory);
    }

    public IEnumerable<SubCategoryLastSum> GetLastSumsBySubCategories(DateTime from, IEnumerable<Guid> subCategoryIds)
    {
        using var db = new LiteDatabase(_databaseConfiguration.Path);
        var collection = db.GetCollection<Transaction>(nameof(Transaction));

        var result = new List<SubCategoryLastSum>();
        foreach (var subCategory in subCategoryIds.Distinct())
        {
            db.BeginTrans();
            var query = collection.Query().Where(t => t.Date >= from && t.SubCategoryId == subCategory).OrderByDescending(t => t.Date).Limit(1);
            var transaction = query.FirstOrDefault();
            if (transaction != null)
            {
                result.Add(new (subCategory, transaction.Sum));
            }
            db.Rollback();
        }

        return result;
    }

    public IEnumerable<SubCategoryLastComments> GetCommentsBySubCategories(DateTime from)
    {
        using var db = new LiteDatabase(_databaseConfiguration.Path);
        var collection = db.GetCollection<Transaction>(nameof(Transaction));

        var query = collection.Query().Where(t => t.Date >= from && !string.IsNullOrEmpty(t.Comment))
            .Select(t => new { t.SubCategoryId, t.Comment });

        var result = query
            .ToList()
            .Distinct()
            .GroupBy(t => t.SubCategoryId.GetValueOrDefault())
            .Select(t => new SubCategoryLastComments(t.Key, t.Where(c => !string.IsNullOrEmpty(c.Comment)).Select(c => c.Comment!).ToList()))
            .ToList();

        return result;
    }

    public IEnumerable<Transaction> GetTransactions(TransactionsFilter filter)
    {
        using var db = new LiteDatabase(_databaseConfiguration.Path);
        var collection = db.GetCollection<Transaction>(nameof(Transaction));

        var query = collection.Query().Where(t => t.Date >= filter.PeriodFrom && t.Date <= filter.PeriodTo);
        
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
        using var db = new LiteDatabase(_databaseConfiguration.Path);
        var collection = db.GetCollection<Transaction>(nameof(Transaction));
        return collection.FindById(id);
    }

    public void DeleteTransaction(Guid id)
    {
        using var db = new LiteDatabase(_databaseConfiguration.Path);
        var collection = db.GetCollection<Transaction>(nameof(Transaction));
        collection.Delete(id);
    }

    public void UpdateTransaction(Transaction transaction)
    {
        using var db = new LiteDatabase(_databaseConfiguration.Path);
        var collection = db.GetCollection<Transaction>(nameof(Transaction));
        collection.Upsert(transaction);
    }

    public void InsertTransactions(IEnumerable<Transaction> transactions)
    {
        using var db = new LiteDatabase(_databaseConfiguration.Path);
        var collection = db.GetCollection<Transaction>(nameof(Transaction));
        collection.InsertBulk(transactions);
    }
}
