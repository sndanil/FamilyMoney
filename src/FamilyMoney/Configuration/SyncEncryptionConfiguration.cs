using System.Text.Json.Serialization;

namespace FamilyMoney.Configuration;

/// <summary>
/// E2E-шифрование объектов синхронизации. Парольная фраза не хранится:
/// в конфигурации лежит только соль и производный ключ этого устройства.
/// </summary>
public sealed class SyncEncryptionConfiguration
{
    public bool Enabled { get; set; }

    /// <summary>Соль PBKDF2 в base64. Не является секретом.</summary>
    public string Salt { get; set; } = string.Empty;

    /// <summary>Производный ключ в base64. Локальный секрет, в QR по умолчанию не попадает.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Проверка парольной фразы: зашифрованная метка.</summary>
    public string KeyCheck { get; set; } = string.Empty;

    [JsonIgnore]
    public bool HasKey => !string.IsNullOrWhiteSpace(Key);
}
