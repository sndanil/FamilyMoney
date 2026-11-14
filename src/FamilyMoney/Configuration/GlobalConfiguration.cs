using FamilyMoney.Utils;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Text.Json;

namespace FamilyMoney.Configuration;

public sealed class GlobalConfiguration : IGlobalConfiguration
{
    private readonly IConfiguration _configuration;
    private RootConfiguration? _rootConfiguration;

    public static string HomeFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FamilyMoney");

#if DEBUG
    public static string UserSettingsPath { get; } = Path.Combine(HomeFolder, "appsettings.debug.json");
#else
    public static string UserSettingsPath { get; } = Path.Combine(HomeFolder, "appsettings.json");
#endif

    public GlobalConfiguration(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public RootConfiguration Get()
    {
        var root = _rootConfiguration ??= _configuration.Get<RootConfiguration>() ?? CreateDefault();

        foreach (var database in root.Databases)
        {
            database?.GetSyncConfiguration();
        }

        return root;
    }

    public DatabaseConfiguration GetSelectedDatabase()
    {
        var root = Get();
        if (root.Databases.Length == 0)
        {
            return CreateDefault().Databases[0];
        }

        var index = root.SelectedDatabaseIndex;
        if (index < 0 || index >= root.Databases.Length)
        {
            index = 0;
        }

        return root.Databases[index];
    }

    public void Save(RootConfiguration configuration)
    {
        // Старые версии приложения читают только S3. Держим это поле
        // синхронизированным с новым блоком Sync.
        foreach (var database in configuration.Databases)
        {
            if (database == null)
            {
                continue;
            }

            var sync = database.GetSyncConfiguration();
            var s3 = sync.Provider == SyncProvider.S3
                ? sync.S3
                : new S3StorageConfiguration();
            s3.Enabled = sync.Provider == SyncProvider.S3 && sync.Enabled;
            database.S3 = s3;
        }

        Directory.CreateDirectory(HomeFolder);
        var json = JsonSerializer.Serialize(configuration, JsonDefaults.Indented);
        File.WriteAllText(UserSettingsPath, json);
        _rootConfiguration = configuration;
    }

    private static RootConfiguration CreateDefault()
    {
        return new RootConfiguration
        {
            Databases =
            [
                new DatabaseConfiguration
                {
                    SyncId = Guid.NewGuid(),
                    Name = "База данных",
                    Path = "database.db",
                    BackupsFolder = "Backups",
                    MaxBackups = 10,
                },
            ],
            Transactions = new TransactionsViewConfiguration
            {
                MaxTransactionsByDate = 100,
            },
        };
    }
}
