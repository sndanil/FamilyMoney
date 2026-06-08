namespace FamilyMoney.Utils;

using FamilyMoney.Configuration;

public static class PathHelper
{
    public static string ToStoredPath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return absolutePath;
        }

        var fullPath = Path.GetFullPath(absolutePath);
        var homeFull = Path.GetFullPath(GlobalConfiguration.HomeFolder);

        if (fullPath.StartsWith(homeFull + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || fullPath.Equals(homeFull, StringComparison.Ordinal))
        {
            var relative = Path.GetRelativePath(homeFull, fullPath);
            return string.IsNullOrEmpty(relative) ? "." : relative;
        }

        return fullPath;
    }

    public static string GetFileName(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? "database.db" : Path.GetFileName(path);
    }
}
