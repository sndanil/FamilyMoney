using System.IO;

namespace FamilyMoney.Configuration;

public sealed class DatabaseConfiguration
{
    public Guid SyncId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string BackupsFolder { get; set; } = string.Empty;

    public int MaxBackups { get; set; } = 10;

    public S3StorageConfiguration S3 { get; set; } = new();

    public string GetResolvedPath()
    {
        return System.IO.Path.IsPathRooted(Path)
            ? Path
            : System.IO.Path.Combine(GlobalConfiguration.HomeFolder, Path);
    }

    public string GetResolvedBackupsFolder()
    {
        return System.IO.Path.IsPathRooted(BackupsFolder)
            ? BackupsFolder
            : System.IO.Path.Combine(GlobalConfiguration.HomeFolder, BackupsFolder);
    }
}
