namespace FamilyMoney.Configuration;

public sealed class SyncStorageConfiguration
{
    public SyncProvider Provider { get; set; } = SyncProvider.None;

    public bool Enabled { get; set; }

    public S3StorageConfiguration S3 { get; set; } = new();

    public WebDavStorageConfiguration WebDav { get; set; } = new();

    public FolderStorageConfiguration Folder { get; set; } = new();

    /// <summary>Заполнены ли обязательные поля выбранного провайдера.</summary>
    public bool HasSettings => Provider switch
    {
        SyncProvider.S3 => !string.IsNullOrWhiteSpace(S3.Bucket)
            && !string.IsNullOrWhiteSpace(S3.AccessKey)
            && !string.IsNullOrWhiteSpace(S3.SecretKey),
        SyncProvider.WebDav => WebDav.IsConfigured,
        SyncProvider.Folder => Folder.IsConfigured,
        _ => false,
    };

    public bool IsConfigured => Enabled && HasSettings;
}
