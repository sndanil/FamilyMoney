using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FamilyMoney.Desktop.Services;
using FamilyMoney.Services;
using FamilyMoney.ViewModels;
using FamilyMoney.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FamilyMoney;

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

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = host.Services.GetRequiredService<MainWindowViewModel>(),
            };
            desktop.Exit += (_, _) => host.Dispose();

            host.RunAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IFilePickerService>(sp =>
        {
            return new AvaloniaFilePickerService(() =>
            {
                if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    return TopLevel.GetTopLevel(desktop.MainWindow);
                }

                return null;
            });
        });
    }
}
