using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Services;
using FamilyMoney.Sync;
using FamilyMoney.Sync.Peer;
using FamilyMoney.State;
using FamilyMoney.ViewModels;
using FamilyMoney.ViewModels.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using System.Globalization;

namespace FamilyMoney;

public static class AppInit
{
    public static IHost? GlobalHost { get; private set; }

    public static IHost InitHost(Action<IServiceCollection> configureDelegate, bool isDesignMode)
    {
        ApplyFormattingCulture();

        GlobalHost = Host
            .CreateDefaultBuilder()
            .ConfigureServices(services => ConfigureServices(services, isDesignMode))
            .ConfigureServices(configureDelegate)
            .ConfigureAppConfiguration((_, config) =>
            {
                var userSettingsPath = GlobalConfiguration.UserSettingsPath;
                // appsettings.json лежит рядом с приложением, а не в рабочем каталоге:
                // при запуске из .desktop-файла или ярлыка рабочий каталог бывает чужим.
                var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                config.AddJsonFile(appSettingsPath, optional: true, reloadOnChange: true)
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

    /// <summary>
    /// Интерфейс приложения русский, поэтому и формат чисел, дат и месяцев русский.
    /// Без этого на машине с английской локалью суммы выглядят как 1,234.56,
    /// а подписи месяцев на графике — как «Nov».
    /// </summary>
    private static void ApplyFormattingCulture()
    {
        var culture = CultureInfo.GetCultureInfo("ru-RU");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
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
            services.AddSingleton<ISyncObjectStoreFactory, SyncObjectStoreFactory>();
        }

        services.AddSingleton<IDatabaseBackup, NullDatabaseBackup>();
        services.AddSingleton<LocalSyncStateStore>();
        services.AddSingleton<SyncImageSynchronizer>();
        services.AddSingleton<SyncOperationGate>();
        services.AddSingleton<SyncConnectionTester>();
        services.AddSingleton<ISyncService, SyncService>();
        services.AddSingleton<PeerCloudPublisher>();
        services.AddTransient<PeerHostViewModel>();

        services.AddTransient<IStateManager, StateManager>();
        services.AddTransient<PeriodViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<AccountsViewModel>();
        services.AddSingleton<TransactionsViewModel>();
        services.AddSingleton<TimelineViewModel>();
        services.AddSingleton<TopExpensesViewModel>();
        services.AddSingleton<TopIncomesViewModel>();
        services.AddSingleton<ReportsViewModel>();
        services.AddSingleton<MainViewModel>();
    }
}
