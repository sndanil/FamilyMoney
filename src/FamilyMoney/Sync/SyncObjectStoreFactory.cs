using FamilyMoney.Configuration;
using Microsoft.Extensions.Logging;

namespace FamilyMoney.Sync;

public sealed class SyncObjectStoreFactory : ISyncObjectStoreFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public SyncObjectStoreFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public ISyncObjectStore Create(SyncStorageConfiguration configuration)
    {
        ISyncObjectStore store = configuration.Provider switch
        {
            SyncProvider.S3 => new S3SyncObjectStore(
                configuration.S3,
                _loggerFactory.CreateLogger<S3SyncObjectStore>()),
            SyncProvider.WebDav => new WebDavSyncObjectStore(
                configuration.WebDav,
                _loggerFactory.CreateLogger<WebDavSyncObjectStore>()),
            SyncProvider.Folder => new FolderSyncObjectStore(
                configuration.Folder,
                _loggerFactory.CreateLogger<FolderSyncObjectStore>()),
            _ => throw new InvalidOperationException("Провайдер синхронизации не выбран."),
        };

        if (!configuration.Encryption.Enabled)
        {
            return store;
        }

        var crypto = SyncCrypto.Create(configuration.Encryption)
            ?? throw new SyncEncryptionException("Не удалось создать ключ шифрования.");
        return new EncryptedSyncObjectStore(store, crypto);
    }
}
