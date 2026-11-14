using FamilyMoney.Configuration;

namespace FamilyMoney.Sync;

public sealed class NullSyncObjectStoreFactory : ISyncObjectStoreFactory
{
    private static readonly NullSyncObjectStore Store = new();

    public ISyncObjectStore Create(SyncStorageConfiguration configuration) => Store;
}
