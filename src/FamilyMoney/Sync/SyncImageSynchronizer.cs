using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Models.Sync;
using Microsoft.Extensions.Logging;

namespace FamilyMoney.Sync;

public sealed class SyncImageSynchronizer
{
    private readonly IRepository _repository;
    private readonly ILogger<SyncImageSynchronizer> _logger;

    public SyncImageSynchronizer(IRepository repository, ILogger<SyncImageSynchronizer> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task UploadPendingAsync(
        DatabaseConfiguration database,
        ISyncObjectStore objectStore,
        IReadOnlyList<SyncImageOutboxEntry> entries,
        CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
        {
            var key = SyncPaths.ImageKey(database, entry.EntityId);
            if (entry.DeletedAt != null)
            {
                await objectStore.DeleteAsync(key, cancellationToken);
                _logger.LogDebug("Deleted remote image {EntityId}", entry.EntityId);
                continue;
            }

            await using var stream = _repository.TryGetImage(entry.EntityId);
            if (stream == null)
            {
                _logger.LogWarning("Local image {EntityId} not found for upload", entry.EntityId);
                continue;
            }

            await objectStore.UploadStreamAsync(key, stream, entry.FileName, cancellationToken);
            _logger.LogDebug("Uploaded image {EntityId} to {Key}", entry.EntityId, key);
        }
    }
}
