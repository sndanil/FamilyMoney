using FamilyMoney.Models;
using FamilyMoney.Models.Sync;

namespace FamilyMoney.Sync;

public static class SyncDeltaBuilder
{
    public static SyncDelta Build(IReadOnlyList<SyncOutboxEntry> entries, long revision, string deviceId)
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

    public static void AddImages(SyncDelta delta, IReadOnlyList<SyncImageOutboxEntry> entries)
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
