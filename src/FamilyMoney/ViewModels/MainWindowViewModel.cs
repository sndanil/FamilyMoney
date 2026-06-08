using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Import;
using FamilyMoney.Messages;
using FamilyMoney.Services;
using FamilyMoney.State;
using FamilyMoney.ViewModels.Settings;
using System.Linq;
using System.Threading.Tasks;

namespace FamilyMoney.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const string AppTitle = "Семейные деньги";

    private readonly IStateManager _stateManager;
    private readonly IRepository _repository;
    private readonly IImporter _importer;
    private readonly IGlobalConfiguration _configuration;

    private readonly PeriodViewModel _period;
    private readonly CategoriesViewModel _categoriesViewModel;
    private readonly AccountsViewModel _accountsViewModel;
    private readonly TransactionsViewModel _transactionsViewModel;

    [ObservableProperty]
    public partial int LeftSideWidth { get; set; } = 500;

    [ObservableProperty]
    public partial bool IsPaneOpen { get; set; }

    [ObservableProperty]
    public partial bool ShowTransactionsTree { get; set; } = true;

    [ObservableProperty]
    public partial MainPanel CurrentPanel { get; set; } = MainPanel.Transactions;

    [ObservableProperty]
    public partial string WindowTitle { get; private set; } = AppTitle;

    public PeriodViewModel Period => _period;

    public SettingsViewModel Settings { get; init; }

    public CategoriesViewModel Categories => _categoriesViewModel;

    public AccountsViewModel Accounts => _accountsViewModel;

    public TransactionsViewModel Transactions => _transactionsViewModel;

    public MainWindowViewModel(
        IRepository repository,
        IStateManager stateManager,
        IImporter importer,
        IGlobalConfiguration configuration,
        PeriodViewModel period,
        CategoriesViewModel categoriesViewModel,
        AccountsViewModel accounts,
        TransactionsViewModel transactionsViewModel,
        SettingsViewModel settingsViewModel)
    {
        _repository = repository;
        _importer = importer;
        _stateManager = stateManager;
        _configuration = configuration;

        Settings = settingsViewModel;
        _categoriesViewModel = categoriesViewModel;
        _accountsViewModel = accounts;
        _transactionsViewModel = transactionsViewModel;
        _period = period;

        PropertyChanged += MainWindowViewModel_PropertyChanged;

        WeakReferenceMessenger.Default.Register<MainWindowViewModel, DatabaseChangedMessage>(this, (_, _) =>
        {
            UpdateWindowTitle();
            MainInit(resetAccountSelection: true);
        });

        UpdateWindowTitle();
        MainInit();

        _repository.DoBackup();
        _repository.UpdateDbSchema();
    }

    private void UpdateWindowTitle()
    {
        var databaseName = _configuration.GetSelectedDatabase().Name;
        WindowTitle = string.IsNullOrWhiteSpace(databaseName)
            ? AppTitle
            : $"{AppTitle} — {databaseName}";
    }

    private void MainWindowViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CurrentPanel) && CurrentPanel == MainPanel.Transactions)
        {
            MainInit();
        }
    }

    private void MainInit(bool resetAccountSelection = false)
    {
        _categoriesViewModel.Reload();

        var accounts = _accountsViewModel.LoadAccounts();
        var state = _stateManager.GetMainState();
        var newState = state with
        {
            Accounts = accounts,
            PeriodFrom = _period.From,
            PeriodTo = _period.To,
            SelectedAccountId = resetAccountSelection ? null : state.SelectedAccountId,
        };
        _stateManager.SetMainState(newState);
    }

    [RelayCommand]
    public async Task TriggerPaneAsync()
    {
        IsPaneOpen = !IsPaneOpen;
        await Task.CompletedTask;
    }

    [RelayCommand]
    public async Task SwitchToAsync(MainPanel panel)
    {
        CurrentPanel = panel;
        await Task.CompletedTask;
    }
}
