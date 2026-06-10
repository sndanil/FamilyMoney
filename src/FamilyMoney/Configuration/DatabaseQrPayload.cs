using FamilyMoney.Utils;
using System.Text.Json;

namespace FamilyMoney.Configuration;

/// <summary>
/// Переносимое представление настроек базы данных и подключения к S3,
/// упаковываемое в QR-код для передачи между устройствами.
/// </summary>
public sealed class DatabaseQrPayload
{
    public const string AppMarker = "familymoney";

    public string App { get; set; } = AppMarker;

    public int Version { get; set; } = 1;

    public Guid SyncId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string BackupsFolder { get; set; } = string.Empty;

    public int MaxBackups { get; set; } = 10;

    public S3StorageConfiguration S3 { get; set; } = new();

    public static DatabaseQrPayload FromConfiguration(DatabaseConfiguration config)
    {
        return new DatabaseQrPayload
        {
            SyncId = config.SyncId,
            Name = config.Name,
            Path = config.Path,
            BackupsFolder = config.BackupsFolder,
            MaxBackups = config.MaxBackups,
            S3 = config.S3,
        };
    }

    public DatabaseConfiguration ToConfiguration()
    {
        return new DatabaseConfiguration
        {
            SyncId = SyncId == Guid.Empty ? Guid.NewGuid() : SyncId,
            Name = Name,
            Path = string.IsNullOrWhiteSpace(Path) ? "database.db" : Path,
            BackupsFolder = string.IsNullOrWhiteSpace(BackupsFolder) ? "Backups" : BackupsFolder,
            MaxBackups = MaxBackups,
            S3 = S3,
        };
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonDefaults.Compact);

    public static DatabaseQrPayload? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<DatabaseQrPayload>(text, JsonDefaults.Compact);
            return payload?.App == AppMarker && !string.IsNullOrWhiteSpace(payload.Name)
                ? payload
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
