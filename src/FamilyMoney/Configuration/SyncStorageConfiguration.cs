namespace FamilyMoney.Configuration;

public sealed class SyncStorageConfiguration
{
    public SyncProvider Provider { get; set; } = SyncProvider.None;

    public bool Enabled { get; set; }

    public S3StorageConfiguration S3 { get; set; } = new();

    public WebDavStorageConfiguration WebDav { get; set; } = new();

    public FolderStorageConfiguration Folder { get; set; } = new();

    public SyncEncryptionConfiguration Encryption { get; set; } = new();

    /// <summary>
    /// Десктоп публикует в облако изменения, принятые по Wi-Fi.
    /// По умолчанию выключено: P2P не должен неожиданно писать в S3.
    /// </summary>
    public bool PublishPeerChangesToCloud { get; set; }

    /// <summary>Заполнены ли обязательные поля выбранного провайдера и ключ шифрования.</summary>
    public bool HasSettings
    {
        get
        {
            var providerConfigured = Provider switch
            {
                SyncProvider.S3 => !string.IsNullOrWhiteSpace(S3.Bucket)
                    && !string.IsNullOrWhiteSpace(S3.AccessKey)
                    && !string.IsNullOrWhiteSpace(S3.SecretKey),
                SyncProvider.WebDav => WebDav.IsConfigured,
                SyncProvider.Folder => Folder.IsConfigured,
                _ => false,
            };

            // Если ключ уже есть, шифрование нельзя выключить: старые объекты
            // в хранилище останутся зашифрованными.
            var encryptionReady = Encryption.Enabled
                ? Encryption.HasKey
                : !Encryption.HasKey;

            return providerConfigured && encryptionReady;
        }
    }

    public bool IsConfigured => Enabled && HasSettings;
}
