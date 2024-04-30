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

    IEnumerable<Account> GetAccounts();
    void UpdateAccount(Account account);
    void DeleteAccount(Guid id);

    IEnumerable<Category> GetCategroties();
    void UpdateCategroty(Category category);

    IEnumerable<SubCategory> GetSubCategroties();
    void UpdateSubCategroty(SubCategory subCategory);

    IEnumerable<Transaction> GetTransactions(DateTime from, DateTime to);
    void UpdateTransaction(Transaction transaction);

    void InsertTransaction(Transaction transaction);

}
