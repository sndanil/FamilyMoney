using FamilyMoney.Utils;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FamilyMoney.Configuration;

/// <summary>
/// Переносимое представление настроек базы данных и синхронизации,
/// упаковываемое в QR-код. Секреты (пароли и ключи) сюда не попадают.
/// </summary>
public sealed class DatabaseQrPayload
{
    public const string AppMarker = "familymoney";

    public string App { get; set; } = AppMarker;

    public int Version { get; set; } = 2;

    public Guid SyncId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string BackupsFolder { get; set; } = string.Empty;

    public int MaxBackups { get; set; } = 10;

    /// <summary>Провайдер и несекретные параметры синхронизации.</summary>
    public SyncStorageConfiguration? Sync { get; set; }

    /// <summary>Старый формат (v1). Читается только для совместимости.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public S3StorageConfiguration? S3 { get; set; }

    /// <summary>
    /// Собирает payload. Секреты попадают в QR только по явному запросу:
    /// это нужно, чтобы настроить телефон, на котором нет формы ввода ключей.
    /// </summary>
    public static DatabaseQrPayload FromConfiguration(
        DatabaseConfiguration config,
        bool includeSecrets = false)
    {
        var sync = config.GetSyncConfiguration();
        return new DatabaseQrPayload
        {
            Version = 2,
            SyncId = config.SyncId,
            Name = config.Name,
            Path = config.Path,
            BackupsFolder = config.BackupsFolder,
            MaxBackups = config.MaxBackups,
            Sync = CreatePortable(sync, includeSecrets),
            // Старые мобильные версии читают только S3. При явной передаче
            // секретов дублируем блок, чтобы ими можно было настроить телефон.
            S3 = includeSecrets && sync.Provider == SyncProvider.S3 ? sync.S3 : null,
        };
    }

    public DatabaseConfiguration ToConfiguration()
    {
        var sync = Sync ?? ConvertLegacyS3();
        var s3 = sync.Provider == SyncProvider.S3
            ? sync.S3
            : new S3StorageConfiguration();

        return new DatabaseConfiguration
        {
            SyncId = SyncId == Guid.Empty ? Guid.NewGuid() : SyncId,
            Name = Name,
            Path = string.IsNullOrWhiteSpace(Path) ? "database.db" : Path,
            BackupsFolder = string.IsNullOrWhiteSpace(BackupsFolder) ? "Backups" : BackupsFolder,
            MaxBackups = MaxBackups,
            Sync = sync,
            S3 = s3,
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

    private SyncStorageConfiguration ConvertLegacyS3()
    {
        var legacy = S3 ?? new S3StorageConfiguration();
        return new SyncStorageConfiguration
        {
            Provider = SyncProvider.S3,
            Enabled = legacy.Enabled,
            S3 = legacy,
        };
    }

    private static SyncStorageConfiguration CreatePortable(
        SyncStorageConfiguration source,
        bool includeSecrets)
    {
        return new SyncStorageConfiguration
        {
            Provider = source.Provider,
            Enabled = source.Enabled,
            S3 = new S3StorageConfiguration
            {
                Enabled = source.Provider == SyncProvider.S3 && source.Enabled,
                ServiceUrl = source.S3.ServiceUrl,
                Bucket = source.S3.Bucket,
                Region = source.S3.Region,
                ForcePathStyle = source.S3.ForcePathStyle,
                AccessKey = includeSecrets ? source.S3.AccessKey : string.Empty,
                SecretKey = includeSecrets ? source.S3.SecretKey : string.Empty,
            },
            WebDav = new WebDavStorageConfiguration
            {
                Url = source.WebDav.Url,
                UserName = source.WebDav.UserName,
                Password = includeSecrets ? source.WebDav.Password : string.Empty,
            },
            Folder = new FolderStorageConfiguration
            {
                Path = source.Folder.Path,
            },
            Encryption = new SyncEncryptionConfiguration
            {
                Enabled = source.Encryption.Enabled,
                Salt = source.Encryption.Salt,
                KeyCheck = source.Encryption.KeyCheck,
                Key = includeSecrets ? source.Encryption.Key : string.Empty,
            },
        };
    }
}
