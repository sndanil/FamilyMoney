using FamilyMoney.Models;
using LiteDB;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace FamilyMoney.DataAccess;

public class DbLiteRepository : IRepository
{
    private string _connectionStr = "database.db";

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
        if (!collection.Update(account))
        {
            collection.Insert(account);
        }
    }

    public void DeleteAccount(Guid id)
    {
        using var db = new LiteDatabase(_connectionStr);
        var collection = db.GetCollection<Account>(nameof(Account));
        collection.Delete(id);
        db.FileStorage.Delete(id.ToString());
    }
}
