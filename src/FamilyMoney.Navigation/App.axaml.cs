using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using FamilyMoney.Services;
using FamilyMoney.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq;

namespace FamilyMoney.Navigation
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            AppInit.InitHost(ConfigureServices, Design.IsDesignMode);
            var host = AppInit.GlobalHost ?? throw new InvalidOperationException("Host initialization failed.");
            var mainViewModel = host.Services.GetRequiredService<MainWindowViewModel>();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow { DataContext = mainViewModel };
                desktop.Exit += (_, _) => host.Dispose();
            }
            else if (ApplicationLifetime is IActivityApplicationLifetime singleViewFactoryApplicationLifetime)
            {
                singleViewFactoryApplicationLifetime.MainViewFactory = () => new PageNavigationHost()
                {
                    Page = new MainView { DataContext = mainViewModel }
                };
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                singleViewPlatform.MainView = new PageNavigationHost()
                {
                    Page = new MainView { DataContext = mainViewModel }
                };
            }

            host.RunAsync();

            base.OnFrameworkInitializationCompleted();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
        }
    }
}