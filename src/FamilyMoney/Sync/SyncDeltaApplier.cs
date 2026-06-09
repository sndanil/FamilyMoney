using FamilyMoney.Models;
using LiteDB;
using Microsoft.Extensions.Logging;

namespace FamilyMoney.Sync;

public sealed class SyncDeltaApplier
{
    private readonly ILogger<SyncDeltaApplier> _logger;

    public SyncDeltaApplier(ILogger<SyncDeltaApplier> logger)
    {
        _logger = logger;
    }

    public void Apply(LiteDatabase db, SyncDelta delta)
    {
        using var _ = SyncContext.EnterApplyScope();

        foreach (var record in delta.Accounts)
        {
            ApplyRecord(db, record, ApplyAccount);
        }

        foreach (var record in delta.Categories)
        {
            ApplyRecord(db, record, ApplyCategory);
        }

        foreach (var record in delta.SubCategories)
        {
            ApplyRecord(db, record, ApplySubCategory);
        }

        foreach (var record in delta.Transactions)
        {
            ApplyRecord(db, record, ApplyTransaction);
        }

        _logger.LogInformation(
            "Applied sync delta {Revision} from device {DeviceId}",
            delta.Revision,
            delta.DeviceId);
    }

    private static void ApplyRecord(LiteDatabase db, SyncEntityRecord record, Action<LiteDatabase, SyncEntityRecord> apply)
    {
        apply(db, record);
    }

    private static void ApplyAccount(LiteDatabase db, SyncEntityRecord record)
    {
        var collection = db.GetCollection<Account>(nameof(Account));
        ApplyToCollection(collection, record);
    }

    private static void ApplyCategory(LiteDatabase db, SyncEntityRecord record)
    {
        var collection = db.GetCollection<Category>(nameof(Category));
        ApplyToCollection(collection, record);
    }

    private static void ApplySubCategory(LiteDatabase db, SyncEntityRecord record)
    {
        var collection = db.GetCollection<SubCategory>(nameof(SubCategory));
        ApplyToCollection(collection, record);
    }

    private static void ApplyTransaction(LiteDatabase db, SyncEntityRecord record)
    {
        var collection = db.GetCollection<Transaction>(nameof(Transaction));
        ApplyToCollection(collection, record);
    }

    private static void ApplyToCollection<T>(ILiteCollection<T> collection, SyncEntityRecord record)
        where T : class, ISyncable
    {
        var existing = collection.FindOne(x => x.Id == record.Id);
        if (record.DeletedAt != null)
        {
            if (existing == null || !ShouldReplace(existing, record))
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

        if (existing != null && !ShouldReplace(existing, record))
        {
            return;
        }

        collection.Upsert(entity);
    }

    private static bool ShouldReplace(ISyncable existing, SyncEntityRecord incoming)
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
}
