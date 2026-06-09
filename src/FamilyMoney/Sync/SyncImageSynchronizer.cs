using FamilyMoney.Configuration;
using LiteDB;
using Microsoft.Extensions.Logging;

namespace FamilyMoney.Sync;

public sealed class SyncImageSynchronizer
{
    private const string MetaCollectionName = "_SyncImageMeta";
    private readonly ILogger<SyncImageSynchronizer> _logger;

    public SyncImageSynchronizer(ILogger<SyncImageSynchronizer> logger)
    {
        _logger = logger;
    }

    public async Task UploadPendingAsync(
        LiteDatabase db,
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

            var file = db.FileStorage.FindById(entry.EntityId.ToString());
            if (file == null)
            {
                _logger.LogWarning("Local image {EntityId} not found for upload", entry.EntityId);
                continue;
            }

            await using var stream = new MemoryStream();
            file.CopyTo(stream);
            stream.Position = 0;
            await objectStore.UploadStreamAsync(key, stream, entry.FileName, cancellationToken);
            _logger.LogDebug("Uploaded image {EntityId} to {Key}", entry.EntityId, key);
        }
    }

    public async Task ApplyAsync(
        LiteDatabase db,
        DatabaseConfiguration database,
        ISyncObjectStore objectStore,
        IEnumerable<SyncImageRecord> images,
        CancellationToken cancellationToken)
    {
        using var _ = SyncContext.EnterApplyScope();
        var metaCollection = db.GetCollection<SyncImageMeta>(MetaCollectionName);

        foreach (var image in images)
        {
            var localMeta = metaCollection.FindOne(x => x.EntityId == image.EntityId);
            if (localMeta != null && !ShouldReplace(localMeta, image))
            {
                continue;
            }

            var storageId = image.EntityId.ToString();
            if (image.DeletedAt != null)
            {
                db.FileStorage.Delete(storageId);
                metaCollection.Upsert(new SyncImageMeta
                {
                    EntityId = image.EntityId,
                    LastChange = image.LastChange,
                    DeletedAt = image.DeletedAt,
                    FileName = image.FileName ?? "image",
                });
                continue;
            }

            var key = SyncPaths.ImageKey(database, image.EntityId);
            await using var stream = await objectStore.DownloadStreamAsync(key, cancellationToken);
            if (stream == null)
            {
                _logger.LogWarning("Remote image {EntityId} not found at {Key}", image.EntityId, key);
                continue;
            }

            var fileName = string.IsNullOrWhiteSpace(image.FileName) ? "image" : image.FileName;
            db.FileStorage.Upload(storageId, fileName, stream);
            metaCollection.Upsert(new SyncImageMeta
            {
                EntityId = image.EntityId,
                LastChange = image.LastChange,
                DeletedAt = null,
                FileName = fileName,
            });
        }
    }

    private static bool ShouldReplace(SyncImageMeta local, SyncImageRecord remote)
    {
        if (remote.LastChange > local.LastChange)
        {
            return true;
        }

        if (remote.LastChange < local.LastChange)
        {
            return false;
        }

        return remote.DeletedAt.HasValue && !local.DeletedAt.HasValue;
    }
}
