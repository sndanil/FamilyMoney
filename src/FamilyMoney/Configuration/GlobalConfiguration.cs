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
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FamilyMoney");

    public GlobalConfiguration(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public RootConfiguration Get()
    {
        return _rootConfiguration ??= _configuration.Get<RootConfiguration>() ?? CreateDefault();
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
        Directory.CreateDirectory(HomeFolder);
        var userSettingsPath = Path.Combine(HomeFolder, "appsettings.json");
        var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(userSettingsPath, json);
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
                    Name = "База данных",
                    Path = Path.Combine(HomeFolder, "database.db"),
                    BackupsFolder = Path.Combine(HomeFolder, "Backups"),
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
