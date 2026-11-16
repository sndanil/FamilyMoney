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

    /// <summary>Каталог данных: база, резервные копии, журналы, снимки синхронизации.</summary>
    public static string HomeFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FamilyMoney");

    /// <summary>
    /// Каталог настроек. На Linux это <c>$XDG_CONFIG_HOME/FamilyMoney</c> (~/.config/FamilyMoney):
    /// по XDG конфигурация и данные живут в разных местах. На остальных платформах —
    /// рядом с данными, как было раньше.
    /// </summary>
    public static string SettingsFolder { get; } = ResolveSettingsFolder();

    public static string UserSettingsPath { get; } = ResolveSettingsPath();

#if DEBUG
    private const string SettingsFileName = "appsettings.debug.json";
#else
    private const string SettingsFileName = "appsettings.json";
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

        var folder = Path.GetDirectoryName(UserSettingsPath);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var json = JsonSerializer.Serialize(configuration, JsonDefaults.Indented);
        File.WriteAllText(UserSettingsPath, json);
        _rootConfiguration = configuration;
    }

    /// <summary>Linux: конфигурация по XDG. Если каталог не определился — остаёмся рядом с данными.</summary>
    private static string ResolveSettingsFolder()
    {
        if (!OperatingSystem.IsLinux())
        {
            return HomeFolder;
        }

        var configHome = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return string.IsNullOrWhiteSpace(configHome)
            ? HomeFolder
            : Path.Combine(configHome, "FamilyMoney");
    }

    /// <summary>
    /// Настройки старого образца лежали рядом с данными. Если новых ещё нет,
    /// переносим файл один раз, а при неудаче продолжаем читать старый путь,
    /// чтобы список баз не потерялся.
    /// </summary>
    private static string ResolveSettingsPath()
    {
        var settings = Path.Combine(SettingsFolder, SettingsFileName);
        if (string.Equals(SettingsFolder, HomeFolder, StringComparison.Ordinal)
            || File.Exists(settings))
        {
            return settings;
        }

        var legacy = Path.Combine(HomeFolder, SettingsFileName);
        if (!File.Exists(legacy))
        {
            return settings;
        }

        try
        {
            Directory.CreateDirectory(SettingsFolder);
            File.Move(legacy, settings);
            return settings;
        }
        catch (Exception)
        {
            return legacy;
        }
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
