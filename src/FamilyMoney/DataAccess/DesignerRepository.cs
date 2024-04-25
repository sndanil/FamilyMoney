using FamilyMoney.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FamilyMoney.DataAccess
{
    internal class DesignerRepository : IRepository
    {
        public IEnumerable<Account> GetAccounts()
        {
            return new [] { new Account { Name = "Тест", Id = Guid.NewGuid() } };
        }

        public Stream? TryGetImage(Guid id)
        {
            return null;
        }

        public void UpdateAccount(Account account)
        {            
        }

        public void UpdateImage(Guid id, string fileName, Stream stream)
        {
        }
    }
}
