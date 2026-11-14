using FamilyMoney.Configuration;

namespace FamilyMoney.Sync;

public interface ISyncObjectStoreFactory
{
    ISyncObjectStore Create(SyncStorageConfiguration configuration);
}
