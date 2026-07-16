using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using FamilyMoney.Services;
using FamilyMoney.State;
using FamilyMoney.Sync;
using FamilyMoney.ViewModels.Settings;
using System.Threading.Tasks;

namespace FamilyMoney.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private const string AppTitle = "Семейные деньги";

    private readonly IStateManager _stateManager;
    private readonly IRepository _repository;
    private readonly IGlobalConfiguration _configuration;
    private readonly ISyncService _syncService;

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
    public partial string Title { get; private set; } = AppTitle;

    public PeriodViewModel Period => _period;

    public SettingsViewModel Settings { get; init; }

    public CategoriesViewModel Categories => _categoriesViewModel;

    public AccountsViewModel Accounts => _accountsViewModel;

    public TransactionsViewModel Transactions => _transactionsViewModel;

    public MainViewModel(
        IRepository repository,
        IStateManager stateManager,
        IGlobalConfiguration configuration,
        ISyncService syncService,
        PeriodViewModel period,
        CategoriesViewModel categoriesViewModel,
        AccountsViewModel accounts,
        TransactionsViewModel transactionsViewModel,
        SettingsViewModel settingsViewModel)
    {
        _repository = repository;
        _stateManager = stateManager;
        _configuration = configuration;
        _syncService = syncService;

        Settings = settingsViewModel;
        _categoriesViewModel = categoriesViewModel;
        _accountsViewModel = accounts;
        _transactionsViewModel = transactionsViewModel;
        _period = period;

        PropertyChanged += MainViewModel_PropertyChanged;

        WeakReferenceMessenger.Default.Register<MainViewModel, DatabaseChangedMessage>(this, (_, _) =>
        {
            UpdateTitle();
            MainInit(resetAccountSelection: true);
            Settings.UpdateSyncStatus();
        });

        UpdateTitle();
        MainInit();
    }

    private void UpdateTitle()
    {
        var databaseName = _configuration.GetSelectedDatabase().Name;
        Title = string.IsNullOrWhiteSpace(databaseName)
            ? AppTitle
            : $"{AppTitle} — {databaseName}";
    }

    private void MainViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CurrentPanel) && CurrentPanel == MainPanel.Transactions)
        {
            MainInit();
        }
    }

    private void MainInit(bool resetAccountSelection = false)
    {
        _repository.UpdateDbSchema();
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
