using FamilyMoney.Configuration;
using FamilyMoney.Models;
using FamilyMoney.Models.Sync;
using FamilyMoney.Sync;
using System;
using System.Collections.Generic;
using System.IO;

namespace FamilyMoney.DataAccess;

public record SubCategoryLastSum(Guid SubCategoryId, decimal Sum);

public record SubCategoryCommentSuggestions(Guid CategoryId, Guid SubCategoryId, IList<string> Comments);

public record SubCategoryTags(Guid SubCategoryId, Guid CategoryId, IList<string> Tags);

public interface IRepository
{
    void UpdateDbSchema();

    /// <summary>
    /// Резервные копии всех настроенных баз. Файл, не изменившийся с прошлой копии, пропускается.
    /// </summary>
    void DoBackup();

    /// <summary>Резервная копия одной базы. Ошибка не прерывает вызывающий сценарий.</summary>
    void DoBackup(DatabaseConfiguration configuration);

    /// <summary>Ручная копия одной базы, даже если файл не менялся с прошлого снимка.</summary>
    DatabaseBackupResult BackupNow(DatabaseConfiguration configuration);

    /// <summary>Ручная копия в выбранный файл. Лимит применяется только к штатной папке копий.</summary>
    DatabaseBackupResult BackupToFile(DatabaseConfiguration configuration, string destinationPath);

    /// <summary>Заменяет файл базы выбранной резервной копией. Прежнее состояние сохраняется в папку копий.</summary>
    DatabaseBackupResult RestoreFromFile(DatabaseConfiguration configuration, string backupPath);
    void UpdateImage(Guid id, string fileName, Stream stream);

    Stream? TryGetImage(Guid id);

    Account GetAccount(Guid id);
    IEnumerable<Account> GetAccounts();
    void UpdateAccount(Account account);
    void DeleteAccount(Guid id);

    /// <summary>
    /// Пересчитывает балансы счетов из транзакций и сохраняет локально без постановки в outbox синхронизации.
    /// </summary>
    void RecalculateAccountBalances();

    Category GetCategory(Guid id);
    IEnumerable<Category> GetCategories();
    void UpdateCategroty(Category category);

    SubCategory GetSubCategory(Guid id);
    SubCategory GetOrCreateSubCategory(Guid? categoryId, string name, Func<SubCategory> factory);
    IEnumerable<SubCategory> GetSubCategories();
    void UpdateSubCategory(SubCategory subCategory);

    /// <summary>
    /// Число неудалённых транзакций по каждой подкатегории. Для неизвестного id возвращается 0.
    /// </summary>
    IReadOnlyDictionary<Guid, int> CountActiveTransactionsBySubCategories(IEnumerable<Guid> subCategoryIds);

    /// <summary>
    /// Переписывает транзакции источников на целевую подкатегорию и помечает источники удалёнными.
    /// </summary>
    void MergeSubCategories(Guid targetId, IReadOnlyCollection<Guid> sourceIds);
    IEnumerable<SubCategoryLastSum> GetLastSumsBySubCategories(DateTime from, IEnumerable<Guid> subCategoryIds);
    IEnumerable<SubCategoryCommentSuggestions> GetCommentSuggestionsBySubCategories(DateTime from);

    IEnumerable<SubCategoryTags> GetTagsBySubCategories(DateTime from);

    IEnumerable<Transaction> GetTransactions(TransactionsFilter filter);
    Transaction? GetTransaction(Guid id);
    void DeleteTransaction(Guid id);
    void UpdateTransaction(Transaction transaction);

    void InsertTransactions(IEnumerable<Transaction> transactions);

    SyncPendingChanges GetPendingSyncChanges();

    void ClearPendingSyncChanges();

    void ApplySyncDelta(SyncDelta delta);

    Task ApplySyncedImagesAsync(
        IEnumerable<SyncImageRecord> images,
        Func<SyncImageRecord, CancellationToken, Task<Stream?>> downloadImageAsync,
        CancellationToken cancellationToken = default);

}
