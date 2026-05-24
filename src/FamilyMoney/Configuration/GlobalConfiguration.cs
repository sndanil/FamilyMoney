using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace FamilyMoney.Configuration;
public class GlobalConfiguration: IGlobalConfiguration
{
    private readonly IConfiguration _configuration;
    private RootConfiguration? _rootConfiguration;

    public static string HomeFolder { get; private set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FamilyMoney");

    public GlobalConfiguration(IConfiguration configuration)
    {
        _configuration = configuration;    
    }

    public RootConfiguration Get()
    {
        return _rootConfiguration ??= _configuration.Get<RootConfiguration>() ?? new RootConfiguration 
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
