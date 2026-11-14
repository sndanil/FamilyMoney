namespace FamilyMoney.Sync;

/// <summary>
/// Manifest одного устройства. Каждое устройство пишет только свой manifest
/// и свой журнал, поэтому устройства не конфликтуют за общий файл.
/// </summary>
public sealed class DeviceSyncManifest
{
    public const int CurrentProtocolVersion = 3;

    public int ProtocolVersion { get; set; } = CurrentProtocolVersion;

    public string DeviceId { get; set; } = string.Empty;

    public long LastSequence { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? LastOperationId { get; set; }
}
