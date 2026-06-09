using FamilyMoney.Models;
using LiteDB;

namespace FamilyMoney.Sync;

public sealed class LiteDbSyncOutbox
{
    private const string CollectionName = "_SyncOutbox";

    public void Enqueue(LiteDatabase db, ISyncable entity)
    {
        if (SyncContext.IsApplying)
        {
            return;
        }

        var collection = db.GetCollection<SyncOutboxEntry>(CollectionName);
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

    public IReadOnlyList<SyncOutboxEntry> GetAll(LiteDatabase db) =>
        db.GetCollection<SyncOutboxEntry>(CollectionName).FindAll().ToList();

    public void Clear(LiteDatabase db) =>
        db.GetCollection<SyncOutboxEntry>(CollectionName).DeleteAll();

    public SyncDelta BuildDelta(IReadOnlyList<SyncOutboxEntry> entries, long revision, string deviceId)
    {
        var delta = new SyncDelta
        {
            Revision = revision,
            DeviceId = deviceId,
            CreatedAt = DateTime.UtcNow,
        };

        foreach (var entry in entries)
        {
            var record = new SyncEntityRecord
            {
                Id = entry.EntityId,
                EntityType = entry.EntityType,
                LastChange = entry.LastChange,
                DeletedAt = entry.DeletedAt,
                DataJson = entry.DataJson,
            };

            switch (SyncEntitySerializer.GetCollectionName(entry.EntityType))
            {
                case nameof(Account):
                    delta.Accounts.Add(record);
                    break;
                case nameof(Category):
                    delta.Categories.Add(record);
                    break;
                case nameof(SubCategory):
                    delta.SubCategories.Add(record);
                    break;
                case nameof(Transaction):
                    delta.Transactions.Add(record);
                    break;
            }
        }

        return delta;
    }
}
