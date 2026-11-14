namespace FamilyMoney.Sync;

/// <summary>Метаданные объекта хранилища, полученные без его содержимого.</summary>
public sealed record SyncObjectInfo(
    string Key,
    long Size,
    string? ETag,
    DateTimeOffset? ModifiedAt);
