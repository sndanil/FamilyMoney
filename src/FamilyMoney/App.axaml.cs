using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Import;
using FamilyMoney.State;
using FamilyMoney.ViewModels;
using FamilyMoney.Views;
using Splat;

namespace FamilyMoney;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        SplatRegistrations.Register<IGlobalConfiguration, MsExtensionConfiguration>();

        if (Avalonia.Controls.Design.IsDesignMode)
        {
            SplatRegistrations.Register<IRepository, DesignerRepository>();
        }
        else
        {
#pragma warning disable SPLATDI006 // Interface has been registered before
            SplatRegistrations.Register<IRepository, LiteDbRepository>();
#pragma warning restore SPLATDI006 // Interface has been registered before
        }

        SplatRegistrations.Register<PeriodViewModel>();
        SplatRegistrations.Register<CategoriesViewModel>();
        SplatRegistrations.Register<AccountsViewModel>();
        SplatRegistrations.Register<TransactionsViewModel>();
        SplatRegistrations.Register<MainWindowViewModel>();
        SplatRegistrations.Register<IImporter, CsvImporter>();
        SplatRegistrations.Register<IStateManager, StateManager>();

        SplatRegistrations.SetupIOC();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Locator.Current.GetService<MainWindowViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}