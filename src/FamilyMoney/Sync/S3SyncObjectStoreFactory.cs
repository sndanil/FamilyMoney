using FamilyMoney.Configuration;
using Microsoft.Extensions.Logging;

namespace FamilyMoney.Sync;

public sealed class S3SyncObjectStoreFactory : ISyncObjectStoreFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public S3SyncObjectStoreFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public ISyncObjectStore Create(S3StorageConfiguration configuration) =>
        new S3SyncObjectStore(configuration, _loggerFactory.CreateLogger<S3SyncObjectStore>());
}
