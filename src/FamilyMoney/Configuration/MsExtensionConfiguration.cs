using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FamilyMoney.Configuration;
public class MsExtensionConfiguration: IGlobalConfiguration
{
    public RootConfiguration Get()
    {
        var userSettingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FamilyMoney");
        var userSettingsPath = Path.Combine(userSettingsDirectory, "appsettings.json");
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile(userSettingsPath, optional: true, reloadOnChange: true)
            .AddCommandLine(System.Environment.GetCommandLineArgs())
            .Build();

        return config.Get<RootConfiguration>() ?? new RootConfiguration 
        { 
            Database = new DatabaseConfiguration 
            { 
                Path = "database.db",
                BackupsFolder = "Backups",
                MaxBackups = 10,
            } 
        };
        //var db = config.GetSection(nameof(DatabaseConfiguration)).Get<DatabaseConfiguration>();
    }
}
