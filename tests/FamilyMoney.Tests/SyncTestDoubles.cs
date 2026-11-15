using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Models;
using FamilyMoney.Models.Sync;
using FamilyMoney.Sync;
using System.Text;

namespace FamilyMoney.Tests;
internal sealed class FakeConfiguration : IGlobalConfiguration
    {
        public FakeConfiguration(DatabaseConfiguration database)
        {
            Root = new RootConfiguration
            {
                Databases = [database],
                SelectedDatabaseIndex = 0,
            };
        }

        public RootConfiguration Root { get; private set; }

        public RootConfiguration Get() => Root;

        public DatabaseConfiguration GetSelectedDatabase() =>
            Root.Databases[Root.SelectedDatabaseIndex];

        public void Save(RootConfiguration configuration) => Root = configuration;
    }

internal sealed class FakeStoreFactory : ISyncObjectStoreFactory
    {
        private readonly ISyncObjectStore _store;

        public FakeStoreFactory(ISyncObjectStore store)
        {
            _store = store;
        }

        public ISyncObjectStore Create(SyncStorageConfiguration configuration)
        {
            if (!configuration.Encryption.Enabled)
            {
                return _store;
            }

            var crypto = SyncCrypto.Create(configuration.Encryption)
                ?? throw new SyncEncryptionException("Не удалось создать ключ шифрования.");
            return new EncryptedSyncObjectStore(_store, crypto);
        }
    }

internal sealed class FakeSyncStore : ISyncObjectStore
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, TextEntry> _text = new(StringComparer.Ordinal);
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

        public bool ConditionalWriteSupported { get; set; } = true;

        public bool SupportsConditionalWrite => ConditionalWriteSupported;

        public Func<string, Task>? BeforeConditionalWrite { get; set; }

        public Task<IReadOnlyList<SyncObjectInfo>> ListAsync(
            string prefix,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                var keys = _text.Keys
                    .Concat(_files.Keys)
                    .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
                    .Select(key =>
                    {
                        var size = _text.TryGetValue(key, out var text)
                            ? Encoding.UTF8.GetByteCount(text.Content)
                            : _files[key].LongLength;
                        var etag = _text.TryGetValue(key, out var entry) ? entry.ETag : null;
                        return new SyncObjectInfo(key, size, etag, DateTimeOffset.UtcNow);
                    })
                    .ToList();

                return Task.FromResult<IReadOnlyList<SyncObjectInfo>>(keys);
            }
        }

        public Task<(string? Content, string? ETag)> TryDownloadTextAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                return Task.FromResult(_text.TryGetValue(key, out var entry)
                    ? (entry.Content, entry.ETag)
                    : ((string?)null, (string?)null));
            }
        }

        public async Task UploadTextAsync(
            string key,
            string content,
            SyncWriteCondition condition,
            CancellationToken cancellationToken = default)
        {
            if (condition.IsConditional && BeforeConditionalWrite != null)
            {
                await BeforeConditionalWrite(key);
            }

            lock (_gate)
            {
                if (ConditionalWriteSupported)
                {
                    if (condition.IfNoneMatch && _text.ContainsKey(key))
                    {
                        throw new SyncConflictException("Object already exists.");
                    }

                    if (!string.IsNullOrWhiteSpace(condition.IfMatchEtag))
                    {
                        if (!_text.TryGetValue(key, out var existing)
                            || !string.Equals(existing.ETag, condition.IfMatchEtag, StringComparison.Ordinal))
                        {
                            throw new SyncConflictException("Object was changed.");
                        }
                    }
                }

                _text[key] = new TextEntry(content, Guid.NewGuid().ToString("N"));
            }
        }

        public Task WriteTextUnconditionalAsync(string key, string content)
        {
            lock (_gate)
            {
                _text[key] = new TextEntry(content, Guid.NewGuid().ToString("N"));
            }

            return Task.CompletedTask;
        }

        public Task UploadFileAsync(
            string key,
            string localFilePath,
            CancellationToken cancellationToken = default)
        {
            var bytes = File.ReadAllBytes(localFilePath);
            lock (_gate)
            {
                _files[key] = bytes;
            }

            return Task.CompletedTask;
        }

        public Task DownloadFileAsync(
            string key,
            string localFilePath,
            CancellationToken cancellationToken = default)
        {
            byte[] bytes;
            lock (_gate)
            {
                if (!_files.TryGetValue(key, out var stored))
                {
                    throw new FileNotFoundException("Sync object was not found.", key);
                }

                bytes = stored;
            }

            var directory = Path.GetDirectoryName(localFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(localFilePath, bytes);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                return Task.FromResult(_text.ContainsKey(key) || _files.ContainsKey(key));
            }
        }

        public async Task UploadStreamAsync(
            string key,
            Stream stream,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            lock (_gate)
            {
                _files[key] = memory.ToArray();
            }
        }

        public Task<Stream?> DownloadStreamAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                return Task.FromResult<Stream?>(_files.TryGetValue(key, out var bytes)
                    ? new MemoryStream(bytes)
                    : null);
            }
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _text.Remove(key);
                _files.Remove(key);
            }

            return Task.CompletedTask;
        }

        public string? GetText(string key)
        {
            lock (_gate)
            {
                return _text.TryGetValue(key, out var entry) ? entry.Content : null;
            }
        }

        public void SetImage(string key, byte[] content)
        {
            lock (_gate)
            {
                _files[key] = content;
            }
        }

        public void SetFile(string key, byte[] content)
        {
            lock (_gate)
            {
                _files[key] = content;
            }
        }

        private sealed record TextEntry(string Content, string ETag);
    }

internal sealed class FakeSyncRepository : IRepository
    {
        public List<SyncOutboxEntry> Entities { get; } = [];

        public List<SyncImageOutboxEntry> ImagesOutbox { get; } = [];

        public Dictionary<Guid, SyncEntityRecord> Applied { get; } = [];

        public Dictionary<Guid, byte[]> Images { get; } = [];

        public bool FailImageDownloads { get; set; }

        public bool ThrowOnClearOnce { get; set; }

        public int CheckpointCount { get; private set; }

        public SyncPendingChanges GetPendingSyncChanges() => new()
        {
            Entities = Entities.ToList(),
            Images = ImagesOutbox.ToList(),
        };

        public SyncChangesBatch GetSyncChangesSince(DateTime since, int maxRecords = 2000) =>
            new(new SyncPendingChanges(), since, since, false);

        public void ClearPendingSyncChanges(SyncPendingChanges pushed)
        {
            if (ThrowOnClearOnce)
            {
                ThrowOnClearOnce = false;
                throw new InvalidOperationException("Simulated failure after publishing the delta.");
            }

            var entityIds = pushed.Entities.Select(entry => entry.Id).ToHashSet();
            Entities.RemoveAll(entry => entityIds.Contains(entry.Id));

            var imageIds = pushed.Images.Select(image => image.Id).ToHashSet();
            ImagesOutbox.RemoveAll(image => imageIds.Contains(image.Id));
        }

        public bool HasLocalData() => Applied.Count > 0 || Entities.Count > 0;

        public void ApplySyncDelta(SyncDelta delta)
        {
            foreach (var record in delta.Accounts
                         .Concat(delta.Categories)
                         .Concat(delta.SubCategories)
                         .Concat(delta.Transactions))
            {
                Applied[record.Id] = record;
            }
        }

        public PeerApplyResult ApplyPeerDelta(SyncDelta delta, bool enqueueForPublish)
        {
            ApplySyncDelta(delta);
            return new PeerApplyResult(
                delta.Accounts
                    .Concat(delta.Categories)
                    .Concat(delta.SubCategories)
                    .Concat(delta.Transactions)
                    .ToList());
        }

        public Task<SyncImagesApplyResult> ApplyPeerImagesAsync(
            IEnumerable<SyncImageRecord> images,
            Func<SyncImageRecord, CancellationToken, Task<Stream?>> downloadImageAsync,
            bool enqueueForPublish,
            CancellationToken cancellationToken = default) =>
            ApplySyncedImagesAsync(images, downloadImageAsync, cancellationToken);

        public async Task<SyncImagesApplyResult> ApplySyncedImagesAsync(
            IEnumerable<SyncImageRecord> images,
            Func<SyncImageRecord, CancellationToken, Task<Stream?>> downloadImageAsync,
            CancellationToken cancellationToken = default)
        {
            var failed = new List<Guid>();

            foreach (var image in images)
            {
                if (image.DeletedAt != null)
                {
                    Images.Remove(image.EntityId);
                    continue;
                }

                if (FailImageDownloads)
                {
                    failed.Add(image.EntityId);
                    continue;
                }

                await using var stream = await downloadImageAsync(image, cancellationToken);
                if (stream == null)
                {
                    failed.Add(image.EntityId);
                    continue;
                }

                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory, cancellationToken);
                Images[image.EntityId] = memory.ToArray();
            }

            return new SyncImagesApplyResult(failed);
        }

        public void Checkpoint() => CheckpointCount++;

        public void UpdateDbSchema()
        {
        }

        public void DoBackup()
        {
        }

        public void DoBackup(DatabaseConfiguration configuration)
        {
        }

        public DatabaseBackupResult BackupNow(DatabaseConfiguration configuration) =>
            new(false, "not supported");

        public DatabaseBackupResult BackupToFile(DatabaseConfiguration configuration, string destinationPath) =>
            new(false, "not supported");

        public DatabaseBackupResult RestoreFromFile(DatabaseConfiguration configuration, string backupPath) =>
            new(false, "not supported");

        public void UpdateImage(Guid id, string fileName, Stream stream)
        {
        }

        public Stream? TryGetImage(Guid id) => null;

        public Account GetAccount(Guid id) => throw new NotImplementedException();

        public IEnumerable<Account> GetAccounts() => [];

        public void UpdateAccount(Account account) => throw new NotImplementedException();

        public void DeleteAccount(Guid id) => throw new NotImplementedException();

        public void RecalculateAccountBalances()
        {
        }

        public Category GetCategory(Guid id) => throw new NotImplementedException();

        public IEnumerable<Category> GetCategories() => [];

        public void UpdateCategroty(Category category) => throw new NotImplementedException();

        public SubCategory GetSubCategory(Guid id) => throw new NotImplementedException();

        public SubCategory GetOrCreateSubCategory(
            Guid? categoryId,
            string name,
            Func<SubCategory> factory) => throw new NotImplementedException();

        public IEnumerable<SubCategory> GetSubCategories() => [];

        public void UpdateSubCategory(SubCategory subCategory) => throw new NotImplementedException();

        public IReadOnlyDictionary<Guid, int> CountActiveTransactionsBySubCategories(
            IEnumerable<Guid> subCategoryIds) => new Dictionary<Guid, int>();

        public void MergeSubCategories(Guid targetId, IReadOnlyCollection<Guid> sourceIds) =>
            throw new NotImplementedException();

        public IEnumerable<SubCategoryLastSum> GetLastSumsBySubCategories(
            DateTime from,
            IEnumerable<Guid> subCategoryIds) => [];

        public IEnumerable<SubCategoryCommentSuggestions> GetCommentSuggestionsBySubCategories(
            DateTime from) => [];

        public IEnumerable<SubCategoryTags> GetTagsBySubCategories(DateTime from) => [];

        public IEnumerable<Transaction> GetTransactions(TransactionsFilter filter) => [];

        public Transaction? GetTransaction(Guid id) => null;

        public void DeleteTransaction(Guid id) => throw new NotImplementedException();

        public void UpdateTransaction(Transaction transaction) => throw new NotImplementedException();

        public void InsertTransactions(IEnumerable<Transaction> transactions) =>
            throw new NotImplementedException();
    }
