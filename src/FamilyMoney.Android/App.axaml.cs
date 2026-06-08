using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FamilyMoney.Android.Services;
using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Import;
using FamilyMoney.Navigation;
using FamilyMoney.Services;
using FamilyMoney.State;
using FamilyMoney.ViewModels;
using FamilyMoney.ViewModels.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog.Extensions.Logging;
using Application = Avalonia.Application;

namespace FamilyMoney.Android;

public partial class App : Application
{
    private IHost? GlobalHost;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        GlobalHost = InitHost();
        var mainViewModel = GlobalHost.Services.GetRequiredService<MainWindowViewModel>();
        var mainView = new MainView { DataContext = mainViewModel };
        GlobalHost.Services.GetRequiredService<MainViewHolder>().View = mainView;

        if (ApplicationLifetime is IActivityApplicationLifetime activityLifetime)
        {
            activityLifetime.MainViewFactory = () => mainView;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewLifetime)
        {
            singleViewLifetime.MainView = mainView;
        }

        GlobalHost.RunAsync();
        base.OnFrameworkInitializationCompleted();
    }

    private static IHost InitHost()
    {
        return Host
            .CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
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
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<MainViewHolder>();
        services.AddSingleton<IGlobalConfiguration, GlobalConfiguration>();
        services.AddSingleton<IFilePickerService>(sp =>
        {
            var mainView = sp.GetRequiredService<MainViewHolder>().View;
            return new AndroidFilePickerService(() => TopLevel.GetTopLevel(mainView));
        });

        if (Design.IsDesignMode)
        {
            services.AddTransient<IRepository, DesignerRepository>();
        }
        else
        {
            services.AddTransient<IRepository, LiteDbRepository>();
        }

        services.AddTransient<IImporter, CsvImporter>();
        services.AddTransient<IStateManager, StateManager>();
        services.AddTransient<PeriodViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<AccountsViewModel>();
        services.AddSingleton<TransactionsViewModel>();
        services.AddSingleton<MainWindowViewModel>();
    }
}
