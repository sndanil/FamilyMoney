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

    private const int DefaultLeftSideWidth = 500;

    private const int DefaultWindowWidth = 1400;

    private const int DefaultWindowHeight = 900;

    private readonly IStateManager _stateManager;
    private readonly IRepository _repository;
    private readonly IDatabaseBackup _databaseBackup;
    private readonly IGlobalConfiguration _configuration;
    private readonly ISyncService _syncService;

    private readonly PeriodViewModel _period;
    private readonly CategoriesViewModel _categoriesViewModel;
    private readonly AccountsViewModel _accountsViewModel;
    private readonly TransactionsViewModel _transactionsViewModel;
    private readonly ReportsViewModel _reportsViewModel;

    [ObservableProperty]
    public partial int LeftSideWidth { get; set; } = DefaultLeftSideWidth;

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

    public ReportsViewModel Reports => _reportsViewModel;

    public MainViewModel(
        IRepository repository,
        IDatabaseBackup databaseBackup,
        IStateManager stateManager,
        IGlobalConfiguration configuration,
        ISyncService syncService,
        PeriodViewModel period,
        CategoriesViewModel categoriesViewModel,
        AccountsViewModel accounts,
        TransactionsViewModel transactionsViewModel,
        ReportsViewModel reportsViewModel,
        SettingsViewModel settingsViewModel)
    {
        _repository = repository;
        _databaseBackup = databaseBackup;
        _stateManager = stateManager;
        _configuration = configuration;
        _syncService = syncService;

        var savedLeftSideWidth = _configuration.Get().AccountsPaneWidth;
        if (savedLeftSideWidth is > 0)
        {
            LeftSideWidth = savedLeftSideWidth.Value;
        }

        Settings = settingsViewModel;
        _categoriesViewModel = categoriesViewModel;
        _accountsViewModel = accounts;
        _transactionsViewModel = transactionsViewModel;
        _reportsViewModel = reportsViewModel;
        _period = period;

        PropertyChanged += MainViewModel_PropertyChanged;

        WeakReferenceMessenger.Default.Register<MainViewModel, DatabaseChangedMessage>(this, (_, _) =>
        {
            UpdateTitle();
            MainInit(resetAccountSelection: true);
            _reportsViewModel.Reload();
            Settings.UpdateSyncStatus();
        });

        UpdateTitle();
        // Снимок до обновления схемы: если миграция повредит файл, останется прежняя копия.
        _databaseBackup.BackupAll();
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
        if (e.PropertyName != nameof(CurrentPanel))
        {
            return;
        }

        if (CurrentPanel == MainPanel.Transactions)
        {
            MainInit();
        }
        else if (CurrentPanel == MainPanel.Categories)
        {
            _categoriesViewModel.EnsureLoaded();
        }
        else if (CurrentPanel == MainPanel.Reports)
        {
            _reportsViewModel.Enter();
        }
    }

    private void MainInit(bool resetAccountSelection = false)
    {
        _repository.UpdateDbSchema();
        _categoriesViewModel.Invalidate();

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

        if (CurrentPanel == MainPanel.Categories)
        {
            _categoriesViewModel.EnsureLoaded();
        }
    }

    /// <summary>Размер главного окна, сохранённый при прошлом закрытии.</summary>
    public (int Width, int Height, bool Maximized) GetWindowPlacement()
    {
        var root = _configuration.Get();
        var hasSavedSize = root.WindowWidth is int width && width > 0
            && root.WindowHeight is int height && height > 0;

        if (!hasSavedSize)
        {
            // Первый запуск: как раньше — развёрнутое окно, без «прыжка» на весь экран.
            return (DefaultWindowWidth, DefaultWindowHeight, true);
        }

        return (root.WindowWidth ?? DefaultWindowWidth, root.WindowHeight ?? DefaultWindowHeight, root.WindowMaximized);
    }

    /// <summary>Сохраняет последнюю раскладку: ширину списка счетов и размер окна.</summary>
    public void SaveLayout(int? windowWidth = null, int? windowHeight = null, bool? maximized = null)
    {
        var root = _configuration.Get();
        var changed = false;

        if (root.AccountsPaneWidth != LeftSideWidth)
        {
            root.AccountsPaneWidth = LeftSideWidth;
            changed = true;
        }

        if (windowWidth is int width && width > 0 && root.WindowWidth != width)
        {
            root.WindowWidth = width;
            changed = true;
        }

        if (windowHeight is int height && height > 0 && root.WindowHeight != height)
        {
            root.WindowHeight = height;
            changed = true;
        }

        if (maximized is bool maximizedValue && root.WindowMaximized != maximizedValue)
        {
            root.WindowMaximized = maximizedValue;
            changed = true;
        }

        if (changed)
        {
            _configuration.Save(root);
        }
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
