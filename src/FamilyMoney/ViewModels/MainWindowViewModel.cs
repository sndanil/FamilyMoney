using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FamilyMoney.DataAccess;
using FamilyMoney.Import;
using FamilyMoney.State;
using System.Linq;
using System.Threading.Tasks;

namespace FamilyMoney.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IStateManager _stateManager;
    private readonly IRepository _repository;
    private readonly IImporter _importer;

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

    public PeriodViewModel Period
    {
        get => _period;
    }

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
        PeriodViewModel period, 
        CategoriesViewModel categoriesViewModel,
        AccountsViewModel accounts, 
        TransactionsViewModel transactionsViewModel)
    {
        _repository = repository;
        _importer = importer;
        _stateManager = stateManager;        

        _categoriesViewModel = categoriesViewModel;
        _accountsViewModel = accounts;
        _transactionsViewModel = transactionsViewModel;
        _period = period;

        PropertyChanged += MainWindowViewModel_PropertyChanged;
        Task.Run(() => 
        {
            _repository.DoBackup();
            _repository.UpdateDbSchema();
        });
    }

    private void MainWindowViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CurrentPanel) && CurrentPanel?.Name == "TransactionsPanel")
        {
            MainInit();
        }
    }

    private void MainInit()
    {
        var accounts = _accountsViewModel.LoadAccounts();
        var state = _stateManager.GetMainState();
        var newState = state with
        {
            Accounts = accounts,
            PeriodFrom = _period.From,
            PeriodTo = _period.To,
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
