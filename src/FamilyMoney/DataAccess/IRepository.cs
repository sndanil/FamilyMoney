using FamilyMoney.Configuration;
using FamilyMoney.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace FamilyMoney.DataAccess;

public record SubCategoryLastSum(Guid SubCategoryId, decimal Sum);

public record SubCategoryLastComments(Guid SubCategoryId, IList<string> Comments);

public record SubCategoryTags(Guid SubCategoryId, Guid CategoryId, IList<string> Tags);

public interface IRepository
{
    void UpdateDbSchema();

    void DoBackup();

    void DoBackup(DatabaseConfiguration configuration);
    void UpdateImage(Guid id, string fileName, Stream stream);

    Stream? TryGetImage(Guid id);

    Account GetAccount(Guid id);
    IEnumerable<Account> GetAccounts();
    void UpdateAccount(Account account);
    void DeleteAccount(Guid id);

    Category GetCategory(Guid id);
    IEnumerable<Category> GetCategories();
    void UpdateCategroty(Category category);

    SubCategory GetSubCategory(Guid id);
    SubCategory GetOrCreateSubCategory(Guid? categoryId, string name, Func<SubCategory> factory);
    IEnumerable<SubCategory> GetSubCategories();
    void UpdateSubCategory(SubCategory subCategory);
    IEnumerable<SubCategoryLastSum> GetLastSumsBySubCategories(DateTime from, IEnumerable<Guid> subCategoryIds);
    IEnumerable<SubCategoryLastComments> GetCommentsBySubCategories(DateTime from);

    IEnumerable<SubCategoryTags> GetTagsBySubCategories(DateTime from);

    IEnumerable<Transaction> GetTransactions(TransactionsFilter filter);
    Transaction? GetTransaction(Guid id);
    void DeleteTransaction(Guid id);
    void UpdateTransaction(Transaction transaction);

    void InsertTransactions(IEnumerable<Transaction> transactions);

}
