using FamilyMoney.Utils;
using System.Security.Cryptography;
using System.Text.Json;

namespace FamilyMoney.Sync.Peer;

/// <summary>
/// QR-пейлоад для подключения телефона к десктопу.
/// Содержит одноразовый токен; не содержит облачных ключей.
/// </summary>
public sealed class PeerPairingPayload
{
    public const string AppName = "familymoney-peer";

    public string App { get; set; } = AppName;

    public int Version { get; set; } = 1;

    public int Protocol { get; set; } = PeerProtocol.Version;

    public Guid SyncId { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }

    public string Fingerprint { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public string DeviceId { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public string ToJson() =>
        JsonSerializer.Serialize(this, JsonDefaults.CamelCaseCompact);

    public static PeerPairingPayload? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<PeerPairingPayload>(
                json,
                JsonDefaults.CamelCaseCompact);
            return payload?.IsValid() == true ? payload : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public bool IsValid() =>
        string.Equals(App, AppName, StringComparison.Ordinal)
        && Version == 1
        && Protocol == PeerProtocol.Version
        && SyncId != Guid.Empty
        && !string.IsNullOrWhiteSpace(Host)
        && Port is > 0 and <= 65535
        && !string.IsNullOrWhiteSpace(Fingerprint)
        && !string.IsNullOrWhiteSpace(Token)
        && !string.IsNullOrWhiteSpace(DeviceId)
        && ExpiresAt > DateTimeOffset.UtcNow;

    public static string CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
