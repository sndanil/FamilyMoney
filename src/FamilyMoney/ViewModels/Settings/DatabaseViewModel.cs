using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Configuration;
using FamilyMoney.Messages;
using FamilyMoney.Services;
using FamilyMoney.Utils;
using System.Threading.Tasks;

namespace FamilyMoney.ViewModels.Settings;

public partial class DatabaseViewModel : ViewModelBase
{
    private readonly IFilePickerService? _filePickerService;

    public DatabaseViewModel(IFilePickerService? filePickerService = null)
    {
        _filePickerService = filePickerService;
    }

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

    [RelayCommand(CanExecute = nameof(CanBrowse))]
    public async Task BrowseDatabasePathAsync()
    {
        if (_filePickerService is null)
        {
            return;
        }

        var path = await _filePickerService.PickSaveDatabaseFileAsync(PathHelper.GetFileName(Path));
        if (path != null)
        {
            Path = PathHelper.ToStoredPath(path);
        }
    }

    [RelayCommand(CanExecute = nameof(CanBrowse))]
    public async Task BrowseBackupsFolderAsync()
    {
        if (_filePickerService is null)
        {
            return;
        }

        var path = await _filePickerService.PickFolderAsync();
        if (path != null)
        {
            BackupsFolder = PathHelper.ToStoredPath(path);
        }
    }

    private bool CanBrowse() => _filePickerService is not null;

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

    public DatabaseViewModel Clone(IFilePickerService? filePickerService = null)
    {
        return new DatabaseViewModel(filePickerService ?? _filePickerService)
        {
            Name = Name,
            Path = Path,
            BackupsFolder = BackupsFolder,
            MaxBackups = MaxBackups,
            IsSelected = IsSelected,
        };
    }

    public static DatabaseViewModel CreateNew(IFilePickerService filePickerService)
    {
        return new DatabaseViewModel(filePickerService)
        {
            Name = "Новая база",
            Path = "database.db",
            BackupsFolder = "Backups",
            MaxBackups = 10,
        };
    }
}
