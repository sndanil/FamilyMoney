using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.State;
using LiteDB;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FamilyMoney.Tests;

public sealed class DatabaseBackupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "FamilyMoneyBackupTests", Guid.NewGuid().ToString("N"));

    public DatabaseBackupTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void RelativeBackupFolder_IsNextToDatabaseFile()
    {
        var directory = Path.Combine(_root, "FamilyMoney");
        var config = Database("Сафоновы", Path.Combine(directory, "Сафоновы.db"), Guid.Parse("dd5b3c52-add9-4bcb-a8ea-04b77080fc1e"));

        Assert.Equal(
            Path.Combine(directory, "Backups", "Сафоновы_dd5b3c52"),
            config.GetResolvedBackupDirectory());
    }

    [Fact]
    public void AbsoluteBackupFolder_StaysAbsoluteAndStillIsolatesDatabase()
    {
        var config = Database("Калитвенцы", Path.Combine(_root, "Калитвенцы.db"), Guid.Parse("a7ca6dd6-52ca-4435-b1bb-4a367a58e15f"));
        config.BackupsFolder = Path.Combine(_root, "absolute");

        Assert.Equal(
            Path.Combine(_root, "absolute", "Калитвенцы_a7ca6dd6"),
            config.GetResolvedBackupDirectory());
    }

    [Fact]
    public void BackupDirectoryName_ReplacesInvalidCharacters()
    {
        var config = Database("A" + Path.DirectorySeparatorChar + "B", Path.Combine(_root, "db.db"), Guid.Empty);

        var directoryName = Path.GetFileName(config.GetResolvedBackupDirectory());

        Assert.Equal("A_B_local", directoryName);
        Assert.All(directoryName, character => Assert.DoesNotContain(character, Path.GetInvalidFileNameChars()));
    }

    [Fact]
    public void DoBackup_WritesEachDatabaseIntoItsOwnFolder()
    {
        var first = Database("Сафоновы", Path.Combine(_root, "Сафоновы.db"), Guid.Parse("dd5b3c52-add9-4bcb-a8ea-04b77080fc1e"));
        var second = Database("Калитвенцы", Path.Combine(_root, "Калитвенцы.db"), Guid.Parse("a7ca6dd6-52ca-4435-b1bb-4a367a58e15f"));
        CreateDatabase(first.Path);
        CreateDatabase(second.Path);
        var parent = Path.Combine(_root, "Backups");
        Directory.CreateDirectory(parent);
        var historical = Path.Combine(parent, "Backup_2026_06_16.db");
        File.WriteAllText(historical, "keep");

        Repository(first, second).DoBackup();

        Assert.Single(Directory.GetFiles(first.GetResolvedBackupDirectory(), "*.db"));
        Assert.Single(Directory.GetFiles(second.GetResolvedBackupDirectory(), "*.db"));
        Assert.Equal("keep", File.ReadAllText(historical));
        using var restored = new LiteDatabase(Directory.GetFiles(first.GetResolvedBackupDirectory(), "*.db")[0]);
        Assert.Equal(1, restored.GetCollection("Item").Count());
    }

    [Fact]
    public void DoBackup_SkipsUnchangedFileAndKeepsLimitPerDatabase()
    {
        var config = Database("Сафоновы", Path.Combine(_root, "Сафоновы.db"), Guid.Parse("dd5b3c52-add9-4bcb-a8ea-04b77080fc1e"));
        config.MaxBackups = 2;
        CreateDatabase(config.Path);
        var repository = Repository(config);

        using (var db = new LiteDatabase(config.Path))
        {
            db.GetCollection("Item").EnsureIndex("Name");
        }

        repository.DoBackup(config);
        repository.DoBackup(config);
        Assert.Single(Directory.GetFiles(config.GetResolvedBackupDirectory(), "*.db"));

        using (var db = new LiteDatabase(config.Path))
        {
            db.GetCollection("Item").EnsureIndex("Name");
        }

        repository.DoBackup(config);
        Assert.Single(Directory.GetFiles(config.GetResolvedBackupDirectory(), "*.db"));

        ChangeDatabase(config.Path);
        repository.DoBackup(config);
        ChangeDatabase(config.Path);
        repository.DoBackup(config);
        ChangeDatabase(config.Path);
        repository.DoBackup(config);

        var files = Directory.GetFiles(config.GetResolvedBackupDirectory(), "*.db");
        Assert.Equal(2, files.Length);
    }

    [Fact]
    public void BackupNow_CreatesCopyEvenWhenDatabaseIsUnchanged()
    {
        var config = Database("Сафоновы", Path.Combine(_root, "Сафоновы.db"), Guid.Parse("dd5b3c52-add9-4bcb-a8ea-04b77080fc1e"));
        config.MaxBackups = 10;
        CreateDatabase(config.Path);
        var repository = Repository(config);

        repository.DoBackup(config);
        var result = repository.BackupNow(config);

        Assert.True(result.Success);
        Assert.Contains("Создана копия", result.Message);
        Assert.Equal(2, Directory.GetFiles(config.GetResolvedBackupDirectory(), "*.db").Length);
    }

    [Fact]
    public void BackupToFile_UsesChosenPathAndKeepsFilesOutsideBackupFolder()
    {
        var config = Database("Сафоновы", Path.Combine(_root, "Сафоновы.db"), Guid.Parse("dd5b3c52-add9-4bcb-a8ea-04b77080fc1e"));
        config.MaxBackups = 1;
        CreateDatabase(config.Path);
        var repository = Repository(config);
        var customDirectory = Path.Combine(_root, "custom");
        Directory.CreateDirectory(customDirectory);
        var neighbor = Path.Combine(customDirectory, "keep.db");
        File.WriteAllText(neighbor, "keep");
        var destination = Path.Combine(customDirectory, "manual.db");

        var result = repository.BackupToFile(config, destination);

        Assert.True(result.Success);
        Assert.True(new FileInfo(destination).Length > 0);
        Assert.Equal("keep", File.ReadAllText(neighbor));
        using var restored = new LiteDatabase(destination);
        Assert.Equal(1, restored.GetCollection("Item").Count());
    }

    [Fact]
    public void BackupToFile_RefusesToOverwriteDatabaseItself()
    {
        var config = Database("Сафоновы", Path.Combine(_root, "Сафоновы.db"), Guid.Parse("dd5b3c52-add9-4bcb-a8ea-04b77080fc1e"));
        CreateDatabase(config.Path);

        var result = Repository(config).BackupToFile(config, config.Path);

        Assert.False(result.Success);
        Assert.Contains("поверх файла базы", result.Message);
    }

    [Fact]
    public void RestoreFromFile_ReplacesDatabaseAndKeepsPreviousState()
    {
        var config = Database("Сафоновы", Path.Combine(_root, "Сафоновы.db"), Guid.Parse("dd5b3c52-add9-4bcb-a8ea-04b77080fc1e"));
        CreateDatabase(config.Path);
        var backup = Path.Combine(_root, "snapshot.db");
        File.Copy(config.Path, backup);
        ChangeDatabase(config.Path);
        Assert.Equal(2, CountItems(config.Path));

        var result = Repository(config).RestoreFromFile(config, backup);

        Assert.True(result.Success);
        Assert.Equal(1, CountItems(config.Path));
        Assert.Equal(1, CountItems(backup));
        var safety = Directory.GetFiles(config.GetResolvedBackupDirectory(), "*.db");
        Assert.Single(safety);
        Assert.Equal(2, CountItems(safety[0]));
    }

    [Fact]
    public void RestoreFromFile_RefusesCurrentDatabaseFile()
    {
        var config = Database("Сафоновы", Path.Combine(_root, "Сафоновы.db"), Guid.Parse("dd5b3c52-add9-4bcb-a8ea-04b77080fc1e"));
        CreateDatabase(config.Path);

        var result = Repository(config).RestoreFromFile(config, config.Path);

        Assert.False(result.Success);
        Assert.Contains("текущей базы", result.Message);
    }

    [Fact]
    public void DoBackup_DoesNotDeleteForeignFilesInDatabaseFolder()
    {
        var config = Database("Сафоновы", Path.Combine(_root, "Сафоновы.db"), Guid.Parse("dd5b3c52-add9-4bcb-a8ea-04b77080fc1e"));
        config.MaxBackups = 1;
        CreateDatabase(config.Path);
        var repository = Repository(config);
        repository.DoBackup(config);

        var note = Path.Combine(config.GetResolvedBackupDirectory(), "note.txt");
        File.WriteAllText(note, "keep");
        ChangeDatabase(config.Path);
        repository.DoBackup(config);

        Assert.Equal("keep", File.ReadAllText(note));
        Assert.Single(Directory.GetFiles(config.GetResolvedBackupDirectory(), "*.db"));
    }

    [Fact]
    public void DoBackup_DisabledOrMissingDatabase_CreatesNothing()
    {
        var missing = Database("Нет", Path.Combine(_root, "missing.db"), Guid.NewGuid());
        var disabled = Database("Выкл", Path.Combine(_root, "disabled.db"), Guid.NewGuid());
        disabled.MaxBackups = 0;
        CreateDatabase(disabled.Path);
        var repository = Repository(missing, disabled);

        repository.DoBackup();

        Assert.False(Directory.Exists(missing.GetResolvedBackupDirectory()));
        Assert.False(Directory.Exists(disabled.GetResolvedBackupDirectory()));
    }

    private static DatabaseConfiguration Database(string name, string path, Guid syncId)
    {
        return new DatabaseConfiguration
        {
            Name = name,
            Path = path,
            SyncId = syncId,
            BackupsFolder = "Backups",
            MaxBackups = 10,
        };
    }

    private static void CreateDatabase(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var db = new LiteDatabase(path);
        db.GetCollection("Item").Insert(new BsonDocument { ["Name"] = "a" });
    }

    private static int CountItems(string path)
    {
        using var db = new LiteDatabase(path);
        return db.GetCollection("Item").Count();
    }

    private static void ChangeDatabase(string path)
    {
        using var db = new LiteDatabase(path);
        db.GetCollection("Item").Insert(new BsonDocument { ["Name"] = Guid.NewGuid().ToString("N") });
    }

    private static LiteDbRepository Repository(params DatabaseConfiguration[] databases)
    {
        return new LiteDbRepository(
            new FixedConfiguration(databases),
            new UnusedAccountSettings(),
            NullLogger<LiteDbRepository>.Instance);
    }

    private sealed class FixedConfiguration(DatabaseConfiguration[] databases) : IGlobalConfiguration
    {
        private RootConfiguration _root = new()
        {
            Databases = databases,
        };

        public RootConfiguration Get() => _root;

        public DatabaseConfiguration GetSelectedDatabase() => _root.Databases[0];

        public void Save(RootConfiguration configuration) => _root = configuration;
    }

    private sealed class UnusedAccountSettings : IAccountLocalSettingsStore
    {
        public bool IsSeeded => true;

        public bool IsHidden(Guid accountId) => false;

        public bool IsExpanded(Guid accountId) => false;

        public void SetHidden(Guid accountId, bool value)
        {
        }

        public void SetExpanded(Guid accountId, bool value)
        {
        }

        public void Seed(IEnumerable<AccountLocalFlags> flags)
        {
        }
    }
}
