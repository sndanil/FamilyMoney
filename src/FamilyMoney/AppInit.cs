using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Services;
using FamilyMoney.Sync;
using FamilyMoney.State;
using FamilyMoney.ViewModels;
using FamilyMoney.ViewModels.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace FamilyMoney;

public static class AppInit
{
    public static IHost? GlobalHost { get; private set; }

    public static IHost InitHost(Action<IServiceCollection> configureDelegate, bool isDesignMode)
    {
        GlobalHost = Host
            .CreateDefaultBuilder()
            .ConfigureServices(services => ConfigureServices(services, isDesignMode))
            .ConfigureServices(configureDelegate)
            .ConfigureAppConfiguration((_, config) =>
            {
                var userSettingsPath = GlobalConfiguration.UserSettingsPath;
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile(userSettingsPath, optional: true, reloadOnChange: true);
                config.AddCommandLine(Environment.GetCommandLineArgs());
            })
            .ConfigureLogging((hostingContext, builder) =>
            {
                builder.AddNLog(hostingContext.Configuration);
            })
            .Build();

        return GlobalHost;
    }

    /// <summary>Снимает резервные копии перед закрытием. Сбой не мешает завершению приложения.</summary>
    public static void BackupDatabases()
    {
        var host = GlobalHost;
        if (host == null)
        {
            return;
        }

        try
        {
            host.Services.GetRequiredService<IDatabaseBackup>().BackupAll();
        }
        catch (Exception ex)
        {
            try
            {
                host.Services.GetService<ILoggerFactory>()
                    ?.CreateLogger(nameof(AppInit))
                    .LogError(ex, "Backup failed");
            }
            catch (Exception)
            {
                // Завершение приложения не должно зависеть от журнала.
            }
        }
    }

    private static void ConfigureServices(IServiceCollection services, bool isDesignMode)
    {
        services.AddSingleton<IGlobalConfiguration, GlobalConfiguration>();
        services.AddSingleton<IAccountLocalSettingsStore, AccountLocalSettingsStore>();

        if (isDesignMode)
        {
            services.AddTransient<IRepository, DesignerRepository>();
            services.AddSingleton<IFilePickerService, NullFilePickerService>();
            services.AddSingleton<ISyncObjectStoreFactory, NullSyncObjectStoreFactory>();
        }
        else
        {
            services.AddTransient<IRepository, LiteDbRepository>();
            services.AddSingleton<ISyncObjectStoreFactory, S3SyncObjectStoreFactory>();
        }

        services.AddSingleton<IDatabaseBackup, NullDatabaseBackup>();
        services.AddSingleton<LocalSyncStateStore>();
        services.AddSingleton<SyncImageSynchronizer>();
        services.AddSingleton<ISyncService, SyncService>();

        services.AddTransient<IStateManager, StateManager>();
        services.AddTransient<PeriodViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<AccountsViewModel>();
        services.AddSingleton<TransactionsViewModel>();
        services.AddSingleton<TimelineViewModel>();
        services.AddSingleton<ReportsViewModel>();
        services.AddSingleton<MainViewModel>();
    }
}
