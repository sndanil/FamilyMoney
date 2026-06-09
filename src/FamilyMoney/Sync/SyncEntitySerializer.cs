using FamilyMoney.Models;
using FamilyMoney.Models.Sync;
using System.Text.Json;

namespace FamilyMoney.Sync;

public static class SyncEntitySerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

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
        return new SyncEntityRecord
        {
            Id = entity.Id,
            EntityType = entity.GetType().Name,
            LastChange = entity.LastChange,
            DeletedAt = entity.DeletedAt,
            DataJson = entity.DeletedAt == null ? JsonSerializer.Serialize(entity, entity.GetType(), JsonOptions) : null,
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

        if (!EntityTypes.TryGetValue(record.EntityType, out var type))
        {
            throw new InvalidOperationException($"Unknown sync entity type: {record.EntityType}");
        }

        return JsonSerializer.Deserialize(record.DataJson, type, JsonOptions);
    }

    public static string GetCollectionName(string entityType) => entityType switch
    {
        nameof(Account) => nameof(Account),
        nameof(DebetCategory) or nameof(CreditCategory) or nameof(TransferCategory) => nameof(Category),
        nameof(DebetSubCategory) or nameof(CreditSubCategory) or nameof(TransferSubCategory) => nameof(SubCategory),
        nameof(DebetTransaction) or nameof(CreditTransaction) or nameof(TransferTransaction) => nameof(Transaction),
        _ => throw new InvalidOperationException($"Unknown sync entity type: {entityType}"),
    };
}
