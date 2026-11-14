namespace FamilyMoney.Sync;

public sealed class LocalSyncState
{
    public Guid DatabaseSyncId { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public long PublishedRevision { get; set; }

    public long AppliedRevision { get; set; }

    /// <summary>Операция, которая уже начала публиковаться. Нужна, чтобы повтор не создал лишнюю ревизию.</summary>
    public string? PendingOperationId { get; set; }

    /// <summary>Хэш отправленных изменений. Если outbox успел измениться, повтор отправит новую дельту.</summary>
    public string? PendingOperationHash { get; set; }
}
