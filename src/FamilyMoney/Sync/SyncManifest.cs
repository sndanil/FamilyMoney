namespace FamilyMoney.Sync;

public sealed class SyncManifest
{
    /// <summary>Версия протокола. 1 — старый формат, 2 — с OperationId и блокировкой.</summary>
    public int ProtocolVersion { get; set; } = 1;

    public long Revision { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string UpdatedByDevice { get; set; } = string.Empty;

    public long? SnapshotRevision { get; set; }

    public string? SnapshotKey { get; set; }

    /// <summary>Идентификатор последней успешной операции push. Нужен для идемпотентного повтора.</summary>
    public string? OperationId { get; set; }
}
