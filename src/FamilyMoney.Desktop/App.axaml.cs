using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Desktop.Services;
using FamilyMoney.Import;
using FamilyMoney.Services;
using FamilyMoney.State;
using FamilyMoney.ViewModels;
using FamilyMoney.ViewModels.Settings;
using FamilyMoney.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog.Extensions.Logging;

namespace FamilyMoney;

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

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = GlobalHost.Services.GetRequiredService<MainWindowViewModel>(),
            };
            desktop.Exit += (_, _) => GlobalHost.Dispose();

            GlobalHost.RunAsync();
        }

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
        services.AddSingleton<IGlobalConfiguration, GlobalConfiguration>();
        services.AddSingleton<IFilePickerService>(sp =>
        {
            return new AvaloniaFilePickerService(() =>
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    return TopLevel.GetTopLevel(desktop.MainWindow);
                }

                return null;
            });
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
