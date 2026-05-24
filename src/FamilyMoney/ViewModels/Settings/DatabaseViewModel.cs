using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Configuration;
using FamilyMoney.Messages;
using System.IO;
using System.Threading.Tasks;

namespace FamilyMoney.ViewModels.Settings;

public partial class DatabaseViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OkCommand))]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OkCommand))]
    public partial string Path { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OkCommand))]
    public partial string BackupsFolder { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int MaxBackups { get; set; } = 10;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    private bool CanOkCommand()
    {
        return !string.IsNullOrWhiteSpace(Name)
            && !string.IsNullOrWhiteSpace(Path)
            && !string.IsNullOrWhiteSpace(BackupsFolder)
            && MaxBackups >= 0;
    }

    [RelayCommand(CanExecute = nameof(CanOkCommand))]
    public async Task OkAsync()
    {
        WeakReferenceMessenger.Default.Send(new ModelCloseMessage<DatabaseViewModel>(this));
        await Task.CompletedTask;
    }

    [RelayCommand]
    public async Task CancelAsync()
    {
        WeakReferenceMessenger.Default.Send(new ModelCloseMessage<DatabaseViewModel>(null));
        await Task.CompletedTask;
    }

    public void FillFrom(DatabaseConfiguration config)
    {
        Name = config.Name;
        Path = config.Path;
        BackupsFolder = config.BackupsFolder;
        MaxBackups = config.MaxBackups;
    }

    public DatabaseConfiguration ToConfiguration()
    {
        return new DatabaseConfiguration
        {
            Name = Name.Trim(),
            Path = Path.Trim(),
            BackupsFolder = BackupsFolder.Trim(),
            MaxBackups = MaxBackups,
        };
    }

    public DatabaseViewModel Clone()
    {
        return new DatabaseViewModel
        {
            Name = Name,
            Path = Path,
            BackupsFolder = BackupsFolder,
            MaxBackups = MaxBackups,
            IsSelected = IsSelected,
        };
    }

    public static DatabaseViewModel CreateNew()
    {
        return new DatabaseViewModel
        {
            Name = "Новая база",
            Path = System.IO.Path.Combine(GlobalConfiguration.HomeFolder, "database.db"),
            BackupsFolder = System.IO.Path.Combine(GlobalConfiguration.HomeFolder, "Backups"),
            MaxBackups = 10,
        };
    }
}
