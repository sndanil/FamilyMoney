using Microsoft.Extensions.Configuration;

namespace FamilyMoney.Configuration;
public class GlobalConfiguration: IGlobalConfiguration
{
    private readonly IConfiguration _configuration;
    private RootConfiguration? _rootConfiguration;

    public GlobalConfiguration(IConfiguration configuration)
    {
        _configuration = configuration;    
    }

    public RootConfiguration Get()
    {
        return _rootConfiguration ??= _configuration.Get<RootConfiguration>() ?? new RootConfiguration 
        { 
            Database = new DatabaseConfiguration
            { 
                Path = "database.db",
                BackupsFolder = "Backups",
                MaxBackups = 10,
            },
            Transactions = new TransactionsViewConfiguration
            {
                MaxTransactionsByDate = 100,
            },
        };
    }
}
