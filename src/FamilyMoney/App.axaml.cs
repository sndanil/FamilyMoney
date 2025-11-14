using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Import;
using FamilyMoney.State;
using FamilyMoney.ViewModels;
using FamilyMoney.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog.Extensions.Logging;
using Splat.Microsoft.Extensions.Logging;
using System;
using System.IO;

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
            desktop.Exit += (sender, args) =>
            {
                GlobalHost.Dispose();
            };

            GlobalHost.RunAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IHost InitHost()
    {
        var host = Host
          .CreateDefaultBuilder()
          .ConfigureServices(services =>
          {
              ConfigureServices(services);
          })
          .ConfigureAppConfiguration((hostingContext, config) =>
          {
              var userSettingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FamilyMoney");
              var userSettingsPath = Path.Combine(userSettingsDirectory, "appsettings.json");
              config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile(userSettingsPath, optional: true, reloadOnChange: true);
              config.AddCommandLine(System.Environment.GetCommandLineArgs());
          })
          .ConfigureLogging((hostingContext, builder) =>
          {
              //builder.AddSplat();
              builder.AddNLog(hostingContext.Configuration);
          })
          .Build();

        return host;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<IGlobalConfiguration, GlobalConfiguration>();

        if (Avalonia.Controls.Design.IsDesignMode)
        {
            services.AddTransient<IRepository, DesignerRepository>();
        }
        else
        {
            services.AddTransient<IRepository, LiteDbRepository>();
        }

        services.AddTransient<PeriodViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<IImporter, CsvImporter>();
        services.AddTransient<IStateManager, StateManager>();

        services.AddSingleton<AccountsViewModel>();
        services.AddSingleton<TransactionsViewModel>();
        services.AddSingleton<MainWindowViewModel>();
    }
}