namespace FamilyMoney.Configuration;

/// <summary>
/// Путь базы в настройках — это строка с того устройства, где базу завели.
/// На другой ОС абсолютный путь чужой системы нельзя ни показывать, ни открывать как файл.
/// </summary>
public static class DatabasePaths
{
    public const string OnDeviceCaption = "На этом устройстве";

    public static bool IsWindowsAbsolute(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path.Length >= 3
            && char.IsLetter(path[0])
            && path[1] == ':'
            && (path[2] == '\\' || path[2] == '/'))
        {
            return true;
        }

        return path.StartsWith(@"\\", StringComparison.Ordinal);
    }

    /// <summary>
    /// Абсолютный путь другой операционной системы: на Android им нельзя пользоваться,
    /// на Windows такой путь из QR как раз указывает на настоящий файл.
    /// </summary>
    public static bool IsForeign(string? path) => IsForeign(path, OperatingSystem.IsWindows());

    public static bool IsForeign(string? path, bool windows)
    {
        return !windows && IsWindowsAbsolute(path);
    }

    public static string LocalFileName(string? storedPath, string? name)
    {
        var segment = LastSegment(storedPath);
        segment = SanitizeFileName(segment);
        if (string.IsNullOrWhiteSpace(segment))
        {
            segment = SanitizeFileName(name);
        }

        if (string.IsNullOrWhiteSpace(segment))
        {
            segment = "database";
        }

        if (!segment.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
        {
            segment += ".db";
        }

        return segment;
    }

    public static string Resolve(string? storedPath, string? name, string home)
    {
        return Resolve(storedPath, name, home, OperatingSystem.IsWindows());
    }

    public static string Resolve(string? storedPath, string? name, string home, bool windows)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return Path.Combine(home, "database.db");
        }

        if (IsForeign(storedPath, windows))
        {
            var local = Path.Combine(home, LocalFileName(storedPath, name));
            if (!OperatingSystem.IsWindows())
            {
                var legacy = LegacyForeignFile(storedPath, home);
                if (!File.Exists(local) && File.Exists(legacy))
                {
                    return legacy;
                }
            }

            return local;
        }

        return Path.IsPathRooted(storedPath)
            ? storedPath
            : Path.Combine(home, storedPath);
    }

    public static string NormalizeBackupFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || IsForeign(folder))
        {
            return "Backups";
        }

        return folder;
    }

    public static string Describe(string? storedPath)
    {
        return Describe(storedPath, OperatingSystem.IsWindows(), GlobalConfiguration.HomeFolder);
    }

    public static string Describe(string? storedPath, bool windows, string home)
    {
        if (string.IsNullOrWhiteSpace(storedPath) || IsForeign(storedPath, windows) || !IsAbsolute(storedPath, windows))
        {
            return OnDeviceCaption;
        }

        if (IsUnder(storedPath, home))
        {
            return OnDeviceCaption;
        }

        if (HasSegment(storedPath, "YandexDisk")
            || HasSegment(storedPath, "Yandex.Disk")
            || HasSegment(storedPath, "Яндекс.Диск"))
        {
            return "Яндекс.Диск";
        }

        if (HasSegmentPrefix(storedPath, "OneDrive"))
        {
            return "OneDrive";
        }

        if (HasSegment(storedPath, "Dropbox"))
        {
            return "Dropbox";
        }

        if (HasSegment(storedPath, "Google Drive") || HasSegment(storedPath, "GoogleDrive"))
        {
            return "Google Диск";
        }

        var folder = LastDirectoryName(storedPath);
        return string.IsNullOrEmpty(folder) ? OnDeviceCaption : "Папка «" + folder + "»";
    }

    /// <summary>
    /// Переписывает чужой абсолютный путь на имя файла в папке приложения
    /// и переносит уже созданный файл, если он лежит под старым именем.
    /// </summary>
    public static bool Adopt(RootConfiguration root, string home)
    {
        var changed = false;
        foreach (var database in root.Databases)
        {
            changed |= AdoptDatabase(database, home);
        }

        return changed;
    }

    public static bool AdoptDatabase(DatabaseConfiguration database, string home)
    {
        return AdoptDatabase(database, home, OperatingSystem.IsWindows());
    }

    public static bool AdoptDatabase(DatabaseConfiguration database, string home, bool windows)
    {
        var changed = false;
        if (IsForeign(database.Path, windows))
        {
            var fileName = LocalFileName(database.Path, database.Name);
            if (!OperatingSystem.IsWindows())
            {
                var legacy = LegacyForeignFile(database.Path, home);
                var resolved = Path.Combine(home, fileName);
                if (File.Exists(legacy) && !File.Exists(resolved) && !MoveIfPresent(legacy, resolved))
                {
                    return changed;
                }
            }

            database.Path = fileName;
            changed = true;
        }

        if (IsForeign(database.BackupsFolder, windows))
        {
            database.BackupsFolder = "Backups";
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Как старый резолвер склеивал чужой путь на Android: вся строка — одно имя файла.
    /// </summary>
    public static string LegacyForeignFile(string storedPath, string home)
    {
        var separator = Path.DirectorySeparatorChar;
        return home.TrimEnd(separator) + separator + storedPath;
    }

    public static bool MoveIfPresent(string source, string destination)
    {
        if (string.IsNullOrWhiteSpace(source)
            || string.IsNullOrWhiteSpace(destination)
            || string.Equals(source, destination, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(source)
            || File.Exists(destination))
        {
            return false;
        }

        var directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Move(source, destination);
        MoveIfPresent(source + "-journal", destination + "-journal");
        return true;
    }

    private static bool IsAbsolute(string path, bool windows)
    {
        if (windows)
        {
            return IsWindowsAbsolute(path) || path.StartsWith('/') || path.StartsWith('\\');
        }

        return path.StartsWith('/');
    }

    private static bool IsUnder(string path, string home)
    {
        if (string.IsNullOrWhiteSpace(home))
        {
            return false;
        }

        var full = path.Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
        var root = home.Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || full.Equals(root, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasSegment(string path, string segment)
    {
        return Segments(path).Any(part => part.Equals(segment, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasSegmentPrefix(string path, string prefix)
    {
        return Segments(path).Any(part => part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string? LastDirectoryName(string path)
    {
        var parts = Segments(path);
        if (parts.Count < 2)
        {
            return null;
        }

        return parts[^2];
    }

    private static string? LastSegment(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var parts = Segments(path);
        return parts.Count == 0 ? null : parts[^1];
    }

    private static List<string> Segments(string path)
    {
        return path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private static string? SanitizeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Trim().ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0)
            {
                chars[i] = '_';
            }
        }

        var safe = new string(chars).Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(safe) ? null : safe;
    }
}
