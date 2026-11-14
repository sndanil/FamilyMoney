namespace FamilyMoney.Sync;

/// <summary>
/// Описание базового снапшота: какие последовательности устройств в него вошли.
/// Новое устройство восстанавливает снапшот и дочитывает только хвосты журналов.
/// </summary>
public sealed class SyncSnapshotMetadata
{
    public const int CurrentProtocolVersion = 3;

    public int ProtocolVersion { get; set; } = CurrentProtocolVersion;

    public DateTime CreatedAt { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public Dictionary<string, long> AppliedByDevice { get; set; } = new(StringComparer.Ordinal);
}
