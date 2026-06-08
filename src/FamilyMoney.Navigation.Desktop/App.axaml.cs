using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FamilyMoney.Navigation;
using FamilyMoney.Navigation.Desktop.Services;
using FamilyMoney.Services;
using FamilyMoney.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FamilyMoney.Navigation.Desktop;

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

        var mainViewModel = host.Services.GetRequiredService<MainViewModel>();
        var mainView = new MainView { DataContext = mainViewModel };
        host.Services.GetRequiredService<MainViewHolder>().View = mainView;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel,
                Content = mainView,
            };
            desktop.Exit += (_, _) => host.Dispose();
        }

        host.RunAsync();
        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<MainViewHolder>();
        services.AddSingleton<IFilePickerService>(sp =>
        {
            var mainView = sp.GetRequiredService<MainViewHolder>().View;
            return new AvaloniaFilePickerService(() => TopLevel.GetTopLevel(mainView));
        });
    }
}
