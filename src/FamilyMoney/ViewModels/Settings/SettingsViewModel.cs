using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace FamilyMoney.ViewModels.Settings;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IGlobalConfiguration _configuration;
    private readonly IRepository _repository;

    public ObservableCollection<DatabaseViewModel> Databases { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditDatabaseCommand), nameof(DeleteDatabaseCommand), nameof(SetActiveDatabaseCommand))]
    public partial DatabaseViewModel? SelectedDatabase { get; set; }

    public SettingsViewModel(IGlobalConfiguration configuration, IRepository repository)
    {
        _configuration = configuration;
        _repository = repository;
        Load();
    }

    private void Load()
    {
        Databases.Clear();
        var root = _configuration.Get();
        var selectedIndex = root.SelectedDatabaseIndex;

        for (var i = 0; i < root.Databases.Length; i++)
        {
            var database = new DatabaseViewModel();
            database.FillFrom(root.Databases[i]);
            database.IsSelected = i == selectedIndex;
            Databases.Add(database);
        }
    }

    [RelayCommand]
    public async Task AddDatabaseAsync()
    {
        var database = DatabaseViewModel.CreateNew();
        var result = await WeakReferenceMessenger.Default.Send(new ModelEditMessage<DatabaseViewModel>(database));
        if (result == null)
        {
            return;
        }

        if (Databases.Count == 0)
        {
            result.IsSelected = true;
        }

        Databases.Add(result);
        Save();
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    public async Task EditDatabaseAsync()
    {
        if (SelectedDatabase == null)
        {
            return;
        }

        var copy = SelectedDatabase.Clone();
        var result = await WeakReferenceMessenger.Default.Send(new ModelEditMessage<DatabaseViewModel>(copy));
        if (result == null)
        {
            return;
        }

        SelectedDatabase.Name = result.Name;
        SelectedDatabase.Path = result.Path;
        SelectedDatabase.BackupsFolder = result.BackupsFolder;
        SelectedDatabase.MaxBackups = result.MaxBackups;
        Save();
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    public async Task DeleteDatabaseAsync()
    {
        if (SelectedDatabase == null || Databases.Count <= 1)
        {
            return;
        }

        var wasSelected = SelectedDatabase.IsSelected;
        Databases.Remove(SelectedDatabase);
        SelectedDatabase = null;

        if (wasSelected)
        {
            Databases[0].IsSelected = true;
        }

        Save();
        await Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    public async Task SetActiveDatabaseAsync()
    {
        if (SelectedDatabase == null)
        {
            return;
        }

        await SwitchToDatabaseAsync(SelectedDatabase);
    }

    [RelayCommand(CanExecute = nameof(CanSwitchToDatabase))]
    public async Task SwitchToDatabaseAsync(DatabaseViewModel? database)
    {
        if (database == null || database.IsSelected)
        {
            return;
        }

        foreach (var item in Databases)
        {
            item.IsSelected = false;
        }

        database.IsSelected = true;
        SelectedDatabase = database;
        Save();
        await Task.CompletedTask;
    }

    private bool CanSwitchToDatabase(DatabaseViewModel? database)
    {
        return database != null && !database.IsSelected;
    }

    private bool CanEditOrDelete()
    {
        return SelectedDatabase != null;
    }

    private bool CanDelete()
    {
        return SelectedDatabase != null && Databases.Count > 1;
    }
    private void Save()
    {
        var root = _configuration.Get();
        var previousIndex = root.SelectedDatabaseIndex;
        var selectedIndex = 0;
        for (var i = 0; i < Databases.Count; i++)
        {
            if (Databases[i].IsSelected)
            {
                selectedIndex = i;
                break;
            }
        }

        if (selectedIndex != previousIndex
            && previousIndex >= 0
            && previousIndex < root.Databases.Length)
        {
            _repository.DoBackup(root.Databases[previousIndex]);
        }

        var newRoot = new RootConfiguration
        {
            Databases = Databases.Select(d => d.ToConfiguration()).ToArray(),
            SelectedDatabaseIndex = selectedIndex,
            Transactions = root.Transactions,
        };

        _configuration.Save(newRoot);

        if (selectedIndex != previousIndex)
        {
            WeakReferenceMessenger.Default.Send(new DatabaseChangedMessage());
        }
    }
}
