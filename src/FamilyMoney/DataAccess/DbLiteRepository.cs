using FamilyMoney.Models;
using LiteDB;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace FamilyMoney.DataAccess;

public class DbLiteRepository : IRepository
{
    private string _connectionStr = "database.db";

    public DbLiteRepository()
    {
        using var db = new LiteDatabase(_connectionStr);
        var transactions = db.GetCollection<Transaction>(nameof(Transaction));
        transactions.EnsureIndex(t => t.Date);
    }

    public void UpdateImage(Guid id, string fileName, Stream stream)
    {
        var strId = id.ToString();
        using var db = new LiteDatabase(_connectionStr);
        db.FileStorage.Upload(strId, fileName, stream);
    }

    public Stream? TryGetImage(Guid id)
    {
        var strId = id.ToString();
        using var db = new LiteDatabase(_connectionStr);
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
        using var db = new LiteDatabase(_connectionStr);
        var collection = db.GetCollection<Account>(nameof(Account));
        return collection.FindOne(c => c.Id == id);
    }

    public IEnumerable<Account> GetAccounts()
    {
        using var db = new LiteDatabase(_connectionStr);
        var collection = db.GetCollection<Account>(nameof(Account));
        return collection.FindAll().ToList();
    }

    public void UpdateAccount(Account account)
    {
        using var db = new LiteDatabase(_connectionStr);
        var collection = db.GetCollection<Account>(nameof(Account));
        collection.Upsert(account);
    }

    public void DeleteAccount(Guid id)
    {
        using var db = new LiteDatabase(_connectionStr);
        var collection = db.GetCollection<Account>(nameof(Account));
        collection.Delete(id);
        db.FileStorage.Delete(id.ToString());
    }

    public void UpdateCategroty(Category category)
    {
        using var db = new LiteDatabase(_connectionStr);
        var collection = db.GetCollection<Category>(nameof(Category));
        collection.Upsert(category);
    }

    public Category GetCategory(Guid id)
    {
        using var db = new LiteDatabase(_connectionStr);
        var collection = db.GetCollection<Category>(nameof(Category));
        return collection.FindOne(c => c.Id == id);
    }

    public IEnumerable<Category> GetCategories()
    {
        using var db = new LiteDatabase(_connectionStr);
        var collection = db.GetCollection<Category>(nameof(Category));
        return collection.FindAll().ToList();
    }

    public SubCategory GetSubCategory(Guid id)
    {
        using var db = new LiteDatabase(_connectionStr);
        var collection = db.GetCollection<SubCategory>(nameof(SubCategory));
        return collection.FindOne(c => c.Id == id);
    }

    public IEnumerable<SubCategory> GetSubCategories()
    {
        using var db = new LiteDatabase(_connectionStr);
        var collection = db.GetCollection<SubCategory>(nameof(SubCategory));
        return collection.FindAll().ToList();
    }

    public void UpdateSubCategroty(SubCategory subCategory)
    {
        using var db = new LiteDatabase(_connectionStr);
        var collection = db.GetCollection<SubCategory>(nameof(SubCategory));
        collection.Upsert(subCategory);
    }

    public IEnumerable<Transaction> GetTransactions(DateTime from, DateTime to)
    {
        using var db = new LiteDatabase(_connectionStr);
        var collection = db.GetCollection<Transaction>(nameof(Transaction));

        return collection.Find(t => t.Date >= from && t.Date <= to)
            .OrderBy(t => t.Date)
            .ToList();
    }

    public void UpdateTransaction(Transaction transaction)
    {
        using var db = new LiteDatabase(_connectionStr);
        var collection = db.GetCollection<Transaction>(nameof(Transaction));
        collection.Upsert(transaction);
    }

    public void InsertTransactions(IEnumerable<Transaction> transactions)
    {
        using var db = new LiteDatabase(_connectionStr);
        var collection = db.GetCollection<Transaction>(nameof(Transaction));
        collection.InsertBulk(transactions);
    }
}
