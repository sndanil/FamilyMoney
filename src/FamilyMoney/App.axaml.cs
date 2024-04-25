using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FamilyMoney.DataAccess;
using FamilyMoney.ViewModels;
using FamilyMoney.Views;
using Splat;

namespace FamilyMoney;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        if (Avalonia.Controls.Design.IsDesignMode)
        {
            SplatRegistrations.RegisterConstant<IRepository>(new DesignerRepository());
        }
        else
        {
            SplatRegistrations.RegisterConstant<IRepository>(new DbLiteRepository());
        }

        SplatRegistrations.SetupIOC();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}