namespace FamilyMoney.Sync.Peer;

/// <summary>
/// Состояние одной пары устройств для прямой синхронизации.
/// <see cref="AppliedFromPeer"/> — максимальный LastChange применённых данных пира;
/// <see cref="PeerAppliedFromUs"/> — последнее подтверждение пира (для статуса).
/// </summary>
public sealed class PeerSyncState
{
    public Guid DatabaseSyncId { get; set; }

    public string PeerDeviceId { get; set; } = string.Empty;

    public string PeerName { get; set; } = string.Empty;

    public DateTime AppliedFromPeer { get; set; }

    public DateTime PeerAppliedFromUs { get; set; }

    /// <summary>
    /// Отметка, с которой отправляются свои изменения. Всё, что не новее,
    /// у пира уже есть (получено от него или подтверждено им).
    /// </summary>
    public DateTime PushFrom { get; set; }

    public DateTime LastSyncAt { get; set; }
}
