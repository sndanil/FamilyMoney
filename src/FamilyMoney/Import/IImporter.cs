using System.IO;

namespace FamilyMoney.Import;

public interface IImporter
{
    void DoImport(Stream stream);
}
