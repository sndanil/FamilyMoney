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
        S3.PropertyChanged += (_, _) => OkCommand.NotifyCanExecuteChanged();
    }

    public S3StorageViewModel S3 { get; } = new();

    [ObservableProperty]
    public partial Guid SyncId { get; set; }

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
        if (string.IsNullOrWhiteSpace(Name)
            || string.IsNullOrWhiteSpace(Path)
            || string.IsNullOrWhiteSpace(BackupsFolder)
            || MaxBackups < 0)
        {
            return false;
        }

        if (!S3.Enabled)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(S3.Bucket)
            && !string.IsNullOrWhiteSpace(S3.AccessKey)
            && !string.IsNullOrWhiteSpace(S3.SecretKey);
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
        SyncId = config.SyncId == Guid.Empty ? Guid.NewGuid() : config.SyncId;
        Name = config.Name;
        Path = config.Path;
        BackupsFolder = config.BackupsFolder;
        MaxBackups = config.MaxBackups;
        S3.FillFrom(config.S3);
    }

    public DatabaseConfiguration ToConfiguration()
    {
        return new DatabaseConfiguration
        {
            SyncId = SyncId == Guid.Empty ? Guid.NewGuid() : SyncId,
            Name = Name.Trim(),
            Path = Path.Trim(),
            BackupsFolder = BackupsFolder.Trim(),
            MaxBackups = MaxBackups,
            S3 = S3.ToConfiguration(),
        };
    }

    public void ApplyFrom(DatabaseViewModel source)
    {
        SyncId = source.SyncId;
        Name = source.Name;
        Path = source.Path;
        BackupsFolder = source.BackupsFolder;
        MaxBackups = source.MaxBackups;
        S3.ApplyFrom(source.S3);
    }

    public DatabaseViewModel Clone(IFilePickerService? filePickerService = null)
    {
        var clone = new DatabaseViewModel(filePickerService ?? _filePickerService)
        {
            SyncId = SyncId,
            Name = Name,
            Path = Path,
            BackupsFolder = BackupsFolder,
            MaxBackups = MaxBackups,
            IsSelected = IsSelected,
        };
        clone.S3.ApplyFrom(S3);
        return clone;
    }

    public static DatabaseViewModel CreateNew(IFilePickerService filePickerService)
    {
        return new DatabaseViewModel(filePickerService)
        {
            SyncId = Guid.NewGuid(),
            Name = "Новая база",
            Path = "database.db",
            BackupsFolder = "Backups",
            MaxBackups = 10,
        };
    }
}
