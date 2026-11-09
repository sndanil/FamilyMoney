using FamilyMoney.Configuration;
using Xunit;

namespace FamilyMoney.Tests;

public class DatabasePathsTests
{
    private const string WindowsPath = @"D:\YandexDisk\FamilyMoney\Сафоновы.db";

    [Fact]
    public void WindowsAbsolutePath_IsForeignOnlyOffWindows()
    {
        Assert.False(DatabasePaths.IsForeign(WindowsPath, windows: true));
        Assert.True(DatabasePaths.IsForeign(WindowsPath, windows: false));
        Assert.False(DatabasePaths.IsForeign("database.db", windows: false));
        Assert.False(DatabasePaths.IsForeign("/data/family.db", windows: false));
    }

    [Fact]
    public void Resolve_KeepsWindowsPathOnWindows()
    {
        var resolved = DatabasePaths.Resolve(WindowsPath, "Сафоновы", @"C:\App", windows: true);

        Assert.Equal(WindowsPath, resolved);
    }

    [Fact]
    public void Resolve_MapsWindowsPathToAppFolderOffWindows()
    {
        var resolved = DatabasePaths.Resolve(WindowsPath, "Сафоновы", "/data/FamilyMoney", windows: false);

        Assert.Equal(Path.Combine("/data/FamilyMoney", "Сафоновы.db"), resolved);
    }

    [Fact]
    public void Resolve_KeepsRelativePathBesideTheApp()
    {
        var resolved = DatabasePaths.Resolve("database.db", "База", "/data/FamilyMoney", windows: false);

        Assert.Equal(Path.Combine("/data/FamilyMoney", "database.db"), resolved);
    }

    [Fact]
    public void Describe_HidesForeignPathAndNamesKnownFolders()
    {
        Assert.Equal("На этом устройстве", DatabasePaths.Describe(WindowsPath, windows: false, "/data/FamilyMoney"));
        Assert.Equal("На этом устройстве", DatabasePaths.Describe("database.db", windows: true, @"C:\App"));
        Assert.Equal("Яндекс.Диск", DatabasePaths.Describe(WindowsPath, windows: true, @"C:\App"));
        Assert.Equal(
            "Папка «Documents»",
            DatabasePaths.Describe(@"C:\Users\me\Documents\family.db", windows: true, @"C:\App"));
        Assert.Equal(
            "На этом устройстве",
            DatabasePaths.Describe(@"C:\App\FamilyMoney\database.db", windows: true, @"C:\App\FamilyMoney"));
    }

    [Fact]
    public void Adopt_RewritesForeignPathToFileName()
    {
        var database = new DatabaseConfiguration
        {
            Name = "Сафоновы",
            Path = WindowsPath,
            BackupsFolder = @"D:\YandexDisk\FamilyMoney\Backups",
        };

        var changed = DatabasePaths.AdoptDatabase(database, "/data/FamilyMoney", windows: false);

        Assert.True(changed);
        Assert.Equal("Сафоновы.db", database.Path);
        Assert.Equal("Backups", database.BackupsFolder);
    }

    [Fact]
    public void Adopt_LeavesNativeWindowsPathUntouched()
    {
        var database = new DatabaseConfiguration
        {
            Name = "Сафоновы",
            Path = WindowsPath,
            BackupsFolder = "Backups",
        };

        var changed = DatabasePaths.AdoptDatabase(database, @"C:\App", windows: true);

        Assert.False(changed);
        Assert.Equal(WindowsPath, database.Path);
    }

    [Fact]
    public void MoveIfPresent_RenamesTheFileOnce()
    {
        var root = Path.Combine(Path.GetTempPath(), "fm-paths-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "legacy.db");
            var destination = Path.Combine(root, "Сафоновы.db");
            File.WriteAllText(source, "data");
            File.WriteAllText(source + "-journal", "journal");

            Assert.True(DatabasePaths.MoveIfPresent(source, destination));
            Assert.False(File.Exists(source));
            Assert.Equal("data", File.ReadAllText(destination));
            Assert.Equal("journal", File.ReadAllText(destination + "-journal"));
            Assert.False(DatabasePaths.MoveIfPresent(source, destination));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
