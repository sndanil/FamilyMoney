using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Configuration;
using FamilyMoney.Messages;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace FamilyMoney.ViewModels.Settings;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IGlobalConfiguration _configuration;

    public ObservableCollection<DatabaseViewModel> Databases { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditDatabaseCommand), nameof(DeleteDatabaseCommand), nameof(SetActiveDatabaseCommand))]
    public partial DatabaseViewModel? SelectedDatabase { get; set; }

    public SettingsViewModel(IGlobalConfiguration configuration)
    {
        _configuration = configuration;
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

        foreach (var database in Databases)
        {
            database.IsSelected = false;
        }

        SelectedDatabase.IsSelected = true;
        Save();
        await Task.CompletedTask;
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
        var selectedIndex = 0;
        for (var i = 0; i < Databases.Count; i++)
        {
            if (Databases[i].IsSelected)
            {
                selectedIndex = i;
                break;
            }
        }

        var newRoot = new RootConfiguration
        {
            Databases = Databases.Select(d => d.ToConfiguration()).ToArray(),
            SelectedDatabaseIndex = selectedIndex,
            Transactions = root.Transactions,
        };

        _configuration.Save(newRoot);
    }
}
