using FamilyMoney.Models;
using FamilyMoney.Models.Sync;
using FamilyMoney.Utils;
using System.Text.Json;

namespace FamilyMoney.Sync;

public static class SyncEntitySerializer
{
    private static readonly Dictionary<string, Type> EntityTypes = new(StringComparer.Ordinal)
    {
        [nameof(Account)] = typeof(Account),
        [nameof(DebetCategory)] = typeof(DebetCategory),
        [nameof(CreditCategory)] = typeof(CreditCategory),
        [nameof(TransferCategory)] = typeof(TransferCategory),
        [nameof(DebetSubCategory)] = typeof(DebetSubCategory),
        [nameof(CreditSubCategory)] = typeof(CreditSubCategory),
        [nameof(TransferSubCategory)] = typeof(TransferSubCategory),
        [nameof(DebetTransaction)] = typeof(DebetTransaction),
        [nameof(CreditTransaction)] = typeof(CreditTransaction),
        [nameof(TransferTransaction)] = typeof(TransferTransaction),
    };

    public static SyncEntityRecord CreateRecord(ISyncable entity)
    {
        string? dataJson = null;
        if (entity.DeletedAt == null)
        {
            dataJson = entity switch
            {
                Account account => JsonSerializer.Serialize(ToAccountSyncModel(account), JsonDefaults.CamelCaseCompact),
                _ => JsonSerializer.Serialize(entity, entity.GetType(), JsonDefaults.CamelCaseCompact),
            };
        }

        return new SyncEntityRecord
        {
            Id = entity.Id,
            EntityType = entity.GetType().Name,
            LastChange = entity.LastChange,
            DeletedAt = entity.DeletedAt,
            DataJson = dataJson,
        };
    }

    public static SyncEntityRecord CreateDeleteRecord(ISyncable entity)
    {
        return new SyncEntityRecord
        {
            Id = entity.Id,
            EntityType = entity.GetType().Name,
            LastChange = entity.LastChange,
            DeletedAt = entity.DeletedAt,
            DataJson = null,
        };
    }

    public static object? Deserialize(SyncEntityRecord record)
    {
        if (record.DeletedAt != null || string.IsNullOrWhiteSpace(record.DataJson))
        {
            return null;
        }

        if (record.EntityType == nameof(Account))
        {
            var syncModel = JsonSerializer.Deserialize<AccountSyncModel>(record.DataJson, JsonDefaults.CamelCaseCompact);
            return syncModel == null ? null : FromAccountSyncModel(syncModel);
        }

        if (!EntityTypes.TryGetValue(record.EntityType, out var type))
        {
            throw new InvalidOperationException($"Unknown sync entity type: {record.EntityType}");
        }

        return JsonSerializer.Deserialize(record.DataJson, type, JsonDefaults.CamelCaseCompact);
    }

    public static string GetCollectionName(string entityType) => entityType switch
    {
        nameof(Account) => nameof(Account),
        nameof(DebetCategory) or nameof(CreditCategory) or nameof(TransferCategory) => nameof(Category),
        nameof(DebetSubCategory) or nameof(CreditSubCategory) or nameof(TransferSubCategory) => nameof(SubCategory),
        nameof(DebetTransaction) or nameof(CreditTransaction) or nameof(TransferTransaction) => nameof(Transaction),
        _ => throw new InvalidOperationException($"Unknown sync entity type: {entityType}"),
    };

    private static AccountSyncModel ToAccountSyncModel(Account account) => new()
    {
        Id = account.Id,
        LastChange = account.LastChange,
        DeletedAt = account.DeletedAt,
        ParentId = account.ParentId,
        Name = account.Name,
        Order = account.Order,
        IsGroup = account.IsGroup,
        IsNotSummable = account.IsNotSummable,
    };

    private static Account FromAccountSyncModel(AccountSyncModel model) => new()
    {
        Id = model.Id,
        LastChange = model.LastChange,
        DeletedAt = model.DeletedAt,
        ParentId = model.ParentId,
        Name = model.Name,
        Order = model.Order,
        IsGroup = model.IsGroup,
        IsNotSummable = model.IsNotSummable,
        Sum = 0,
    };
}
