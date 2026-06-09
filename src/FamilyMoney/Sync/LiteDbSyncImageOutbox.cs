using LiteDB;

namespace FamilyMoney.Sync;

public sealed class LiteDbSyncImageOutbox
{
    private const string CollectionName = "_SyncImageOutbox";

    public void EnqueueUpload(LiteDatabase db, Guid entityId, string fileName)
    {
        if (SyncContext.IsApplying)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var collection = db.GetCollection<SyncImageOutboxEntry>(CollectionName);
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

    public void EnqueueDelete(LiteDatabase db, Guid entityId)
    {
        if (SyncContext.IsApplying)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var collection = db.GetCollection<SyncImageOutboxEntry>(CollectionName);
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

    public IReadOnlyList<SyncImageOutboxEntry> GetAll(LiteDatabase db) =>
        db.GetCollection<SyncImageOutboxEntry>(CollectionName).FindAll().ToList();

    public void Clear(LiteDatabase db) =>
        db.GetCollection<SyncImageOutboxEntry>(CollectionName).DeleteAll();

    public void AddToDelta(SyncDelta delta, IReadOnlyList<SyncImageOutboxEntry> entries)
    {
        foreach (var entry in entries)
        {
            delta.Images.Add(new SyncImageRecord
            {
                EntityId = entry.EntityId,
                LastChange = entry.LastChange,
                DeletedAt = entry.DeletedAt,
                FileName = entry.FileName,
            });
        }
    }
}
