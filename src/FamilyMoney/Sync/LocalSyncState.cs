namespace FamilyMoney.Sync;

public sealed class LocalSyncState
{
    public Guid DatabaseSyncId { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public long PublishedRevision { get; set; }

    public long AppliedRevision { get; set; }

    /// <summary>Докуда применён журнал каждого устройства (deviceId → sequence).</summary>
    public Dictionary<string, long> AppliedByDevice { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Докуда применён старый общий журнал до перехода на протокол 3.</summary>
    public long LegacyAppliedRevision { get; set; }

    /// <summary>Старый общий журнал уже перенесён в снапшот/новый протокол.</summary>
    public bool LegacyMigrated { get; set; }

    /// <summary>Собственная последовательность, на которой в последний раз делался снапшот.</summary>
    public long LastSnapshotOwnSequence { get; set; }

    /// <summary>Операция, которая уже начала публиковаться. Нужна, чтобы повтор не создал лишнюю ревизию.</summary>
    public string? PendingOperationId { get; set; }

    /// <summary>Хэш отправленных изменений. Если outbox успел измениться, повтор отправит новую дельту.</summary>
    public string? PendingOperationHash { get; set; }
}
