using FamilyMoney.DataAccess;
using System.IO;

namespace FamilyMoney.Import;

public interface IImporter
{
    void DoImport(IRepository repository, Stream stream);
}
