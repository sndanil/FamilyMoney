using FamilyMoney.Configuration;

namespace FamilyMoney.DataAccess;

/// <summary>
/// Создание резервных копий. В сборках, кроме обычного настольного приложения, подставляется пустая реализация.
/// </summary>
public readonly record struct DatabaseBackupResult(bool Success, string Message);

public interface IDatabaseBackup
{
    void BackupAll();

    void Backup(DatabaseConfiguration configuration);

    /// <summary>Ручная копия одной базы. Новый файл создаётся, даже если содержимое не менялось.</summary>
    DatabaseBackupResult BackupNow(DatabaseConfiguration configuration);

    /// <summary>Ручная копия в файл, выбранный в диалоге.</summary>
    DatabaseBackupResult BackupToFile(DatabaseConfiguration configuration, string destinationPath);

    /// <summary>Восстановление базы из выбранного файла.</summary>
    DatabaseBackupResult RestoreFromFile(DatabaseConfiguration configuration, string backupPath);
}

public sealed class RepositoryDatabaseBackup : IDatabaseBackup
{
    private readonly IRepository _repository;

    public RepositoryDatabaseBackup(IRepository repository)
    {
        _repository = repository;
    }

    public void BackupAll() => _repository.DoBackup();

    public void Backup(DatabaseConfiguration configuration) => _repository.DoBackup(configuration);

    public DatabaseBackupResult BackupNow(DatabaseConfiguration configuration) => _repository.BackupNow(configuration);

    public DatabaseBackupResult BackupToFile(DatabaseConfiguration configuration, string destinationPath)
        => _repository.BackupToFile(configuration, destinationPath);

    public DatabaseBackupResult RestoreFromFile(DatabaseConfiguration configuration, string backupPath)
        => _repository.RestoreFromFile(configuration, backupPath);
}

public sealed class NullDatabaseBackup : IDatabaseBackup
{
    public void BackupAll()
    {
    }

    public void Backup(DatabaseConfiguration configuration)
    {
    }

    public DatabaseBackupResult BackupNow(DatabaseConfiguration configuration)
    {
        return new DatabaseBackupResult(false, "Резервная копия в этой версии не создаётся.");
    }

    public DatabaseBackupResult BackupToFile(DatabaseConfiguration configuration, string destinationPath)
    {
        return new DatabaseBackupResult(false, "Резервная копия в этой версии не создаётся.");
    }

    public DatabaseBackupResult RestoreFromFile(DatabaseConfiguration configuration, string backupPath)
    {
        return new DatabaseBackupResult(false, "Восстановление в этой версии недоступно.");
    }
}
