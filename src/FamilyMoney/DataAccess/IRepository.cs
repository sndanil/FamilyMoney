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

    void UpdateAccount(Account account);
}
