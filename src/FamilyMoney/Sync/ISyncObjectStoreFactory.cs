using FamilyMoney.Configuration;

namespace FamilyMoney.Sync;

public interface ISyncObjectStoreFactory
{
    ISyncObjectStore Create(S3StorageConfiguration configuration);
}
