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
        Sync.PropertyChanged += (_, _) => OkCommand.NotifyCanExecuteChanged();
    }

    public SyncStorageViewModel Sync { get; } = new();

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

    /// <summary>Передавать ключи и пароли в QR. По умолчанию выключено.</summary>
    [ObservableProperty]
    public partial bool IncludeSecretsInQr { get; set; }

    /// <summary>Каталог, в который реально пишутся копии этой базы.</summary>
    public string BackupDirectory => ToConfiguration().GetResolvedBackupDirectory();

    public string BackupLimitText => MaxBackups <= 0 ? "не создаются" : $"до {MaxBackups}";

    /// <summary>Куда положен файл: «На этом устройстве», «Яндекс.Диск» и т.п., без сырого пути.</summary>
    public string LocationText => DatabasePaths.Describe(Path);

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(BackupDirectory));

    partial void OnPathChanged(string value)
    {
        OnPropertyChanged(nameof(BackupDirectory));
        OnPropertyChanged(nameof(LocationText));
    }

    partial void OnBackupsFolderChanged(string value) => OnPropertyChanged(nameof(BackupDirectory));

    partial void OnSyncIdChanged(Guid value) => OnPropertyChanged(nameof(BackupDirectory));

    partial void OnMaxBackupsChanged(int value) => OnPropertyChanged(nameof(BackupLimitText));

    /// <summary>Вкладка, которая открывается при редактировании: 0 — основное, 1 — синхронизация.</summary>
    public int InitialTabIndex { get; set; }

    private bool CanOkCommand()
    {
        if (string.IsNullOrWhiteSpace(Name)
            || string.IsNullOrWhiteSpace(Path)
            || string.IsNullOrWhiteSpace(BackupsFolder)
            || MaxBackups < 0)
        {
            return false;
        }

        return Sync.IsValid;
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

    [RelayCommand(CanExecute = nameof(CanBrowse))]
    public async Task BrowseSyncFolderAsync()
    {
        if (_filePickerService is null)
        {
            return;
        }

        var path = await _filePickerService.PickFolderAsync();
        if (path != null)
        {
            Sync.Folder.Path = PathHelper.ToStoredPath(path);
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
        Sync.FillFrom(config.GetSyncConfiguration());
    }

    public DatabaseConfiguration ToConfiguration()
    {
        var sync = Sync.ToConfiguration();
        return new DatabaseConfiguration
        {
            SyncId = SyncId == Guid.Empty ? Guid.NewGuid() : SyncId,
            Name = Name.Trim(),
            Path = Path.Trim(),
            BackupsFolder = BackupsFolder.Trim(),
            MaxBackups = MaxBackups,
            Sync = sync,
            S3 = sync.Provider == SyncProvider.S3
                ? sync.S3
                : new S3StorageConfiguration(),
        };
    }

    public void ApplyFrom(DatabaseViewModel source)
    {
        SyncId = source.SyncId;
        Name = source.Name;
        Path = source.Path;
        BackupsFolder = source.BackupsFolder;
        MaxBackups = source.MaxBackups;
        Sync.ApplyFrom(source.Sync);
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
            InitialTabIndex = InitialTabIndex,
        };
        clone.Sync.ApplyFrom(Sync);
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
