using FamilyMoney.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace FamilyMoney.DataAccess;

internal class DesignerRepository : IRepository
{
    public void DeleteAccount(Guid id)
    {
        throw new NotImplementedException();
    }

    public void DeleteTransaction(Guid id)
    {
        throw new NotImplementedException();
    }

    public Account GetAccount(Guid id)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<Account> GetAccounts()
    {
        return new [] { new Account { Name = "Тест", Id = Guid.NewGuid() } };
    }

    public IEnumerable<Category> GetCategories()
    {
        throw new NotImplementedException();
    }

    public Category GetCategory(Guid id)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<SubCategoryLastComments> GetCommentsBySubCategories(DateTime from)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<SubCategoryLastSum> GetLastSumsBySubCategories(DateTime from, IEnumerable<Guid> subCategoryIds)
    {
        throw new NotImplementedException();
    }

    public SubCategory GetOrCreateSubCategory(Guid? categoryId, string name, Func<SubCategory> factory)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<SubCategory> GetSubCategories()
    {
        throw new NotImplementedException();
    }

    public SubCategory GetSubCategory(Guid id)
    {
        throw new NotImplementedException();
    }

    public Transaction? GetTransaction(Guid id)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<Transaction> GetTransactions(TransactionsFilter filter)
    {
        throw new NotImplementedException();
    }

    public void InsertTransactions(IEnumerable<Transaction> transactions)
    {
        throw new NotImplementedException();
    }

    public Stream? TryGetImage(Guid id)
    {
        return null;
    }

    public void UpdateAccount(Account account)
    {            
    }

    public void UpdateCategroty(Category category)
    {
        throw new NotImplementedException();
    }

    public void UpdateImage(Guid id, string fileName, Stream stream)
    {
    }

    public void UpdateSubCategory(SubCategory subCategory)
    {
        throw new NotImplementedException();
    }

    public void UpdateTransaction(Transaction transaction)
    {
        throw new NotImplementedException();
    }
}
