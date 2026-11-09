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

    /// <summary>
    /// Каталог резервных копий. Относительный путь считается от папки файла базы,
    /// чтобы копии лежали рядом с ней, а не в каталоге приложения.
    /// </summary>
    public string GetResolvedBackupsFolder()
    {
        var folder = string.IsNullOrWhiteSpace(BackupsFolder) ? "Backups" : BackupsFolder;
        if (System.IO.Path.IsPathRooted(folder))
        {
            return folder;
        }

        var databaseDirectory = System.IO.Path.GetDirectoryName(GetResolvedPath());
        if (string.IsNullOrEmpty(databaseDirectory))
        {
            databaseDirectory = GlobalConfiguration.HomeFolder;
        }

        return System.IO.Path.Combine(databaseDirectory, folder);
    }

    /// <summary>
    /// Подкаталог конкретной базы внутри папки резервных копий: «Имя_xxxxxxxx».
    /// </summary>
    public string GetResolvedBackupDirectory()
    {
        return System.IO.Path.Combine(GetResolvedBackupsFolder(), GetBackupDirectoryName());
    }

    private string GetBackupDirectoryName()
    {
        var name = string.IsNullOrWhiteSpace(Name) ? "database" : Name.Trim();
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0)
            {
                chars[i] = '_';
            }
        }

        var safe = new string(chars).Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "database";
        }

        if (safe.Length > 40)
        {
            safe = safe[..40].TrimEnd();
        }

        var suffix = SyncId == Guid.Empty ? "local" : SyncId.ToString("N")[..8];
        return safe + "_" + suffix;
    }

    public string GetSuggestedBackupFileName(DateTime timestamp)
    {
        return timestamp.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".db";
    }
}
