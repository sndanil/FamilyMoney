using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Import;
using FamilyMoney.Messages;
using FamilyMoney.State;
using CommunityToolkit.Mvvm.Messaging;
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
    public partial Control? CurrentPanel { get; set; }

    [ObservableProperty]
    public partial string WindowTitle { get; private set; } = AppTitle;

    public PeriodViewModel Period
    {
        get => _period;
    }

    public SettingsViewModel Settings { get; init; }

    public CategoriesViewModel Categories
    {
        get => _categoriesViewModel;
    }

    public AccountsViewModel Accounts
    {
        get => _accountsViewModel;
    }

    public TransactionsViewModel Transactions
    {
        get => _transactionsViewModel;
    }

    public MainWindowViewModel(IRepository repository, 
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
            MainInit(resetAccountSelection: true, reloadCategories: true);
        });

        UpdateWindowTitle();

        Task.Run(() => 
        {
            _repository.DoBackup();
            _repository.UpdateDbSchema();
        });
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
        if (e.PropertyName == nameof(CurrentPanel) && CurrentPanel?.Name == "TransactionsPanel")
        {
            MainInit();
        }
    }

    private void MainInit(bool resetAccountSelection = false, bool reloadCategories = false)
    {
        if (reloadCategories)
        {
            _categoriesViewModel.Reload();
        }

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
    public async Task ImportAsync(Avalonia.Visual visual)
    {
        var topLevel = TopLevel.GetTopLevel(visual);
        var files = await topLevel!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выбор файла для импорта",
            AllowMultiple = false,
            FileTypeFilter = [new("Файл CSV") { Patterns = ["*.csv"], MimeTypes = ["*/*"] }, FilePickerFileTypes.All]
        });

        if (files.Any())
        {
            await using var stream = await files.Single().OpenReadAsync();
            _importer.DoImport(stream);
            MainInit();
        }
    }

    [RelayCommand]
    public async Task TriggerPaneAsync()
    {         
        IsPaneOpen = !IsPaneOpen;
        await Task.CompletedTask;
    }

    [RelayCommand]
    public async Task SwitchToAsync(Control control)
    {
        CurrentPanel = control;
    }
}
