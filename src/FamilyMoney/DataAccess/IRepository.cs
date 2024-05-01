using FamilyMoney.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FamilyMoney.DataAccess;

public interface IRepository
{
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
    IEnumerable<SubCategory> GetSubCategories();
    void UpdateSubCategroty(SubCategory subCategory);

    IEnumerable<Transaction> GetTransactions(DateTime from, DateTime to);
    void UpdateTransaction(Transaction transaction);

    void InsertTransactions(IEnumerable<Transaction> transactions);

}
