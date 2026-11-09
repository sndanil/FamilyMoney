using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using FamilyMoney.Services;
using FamilyMoney.Sync;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace FamilyMoney.ViewModels.Settings;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IGlobalConfiguration _configuration;
    private readonly IDatabaseBackup _databaseBackup;
    private readonly IFilePickerService _filePickerService;
    private readonly ISyncService _syncService;
    private readonly IQrScannerService? _qrScanner;

    public ObservableCollection<DatabaseViewModel> Databases { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(
        nameof(EditDatabaseCommand),
        nameof(ConfigureS3Command),
        nameof(DeleteDatabaseCommand),
        nameof(SetActiveDatabaseCommand),
        nameof(BackupNowCommand),
        nameof(RestoreBackupCommand))]
    public partial DatabaseViewModel? SelectedDatabase { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SyncNowCommand))]
    public partial bool IsSyncing { get; set; }

    [ObservableProperty]
    public partial string SyncStatus { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BackupNowCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreBackupCommand))]
    public partial bool IsBackingUp { get; set; }

    [ObservableProperty]
    public partial string BackupStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial decimal DepositWarningDays { get; set; } = RootConfiguration.DefaultDepositWarningDays;

    private bool _suppressDepositWarningWrite;

    public bool IsSyncAvailable => _syncService.IsEnabled;

    public bool IsQrImportAvailable => _qrScanner != null;

    public SettingsViewModel(
        IGlobalConfiguration configuration,
        IDatabaseBackup databaseBackup,
        IFilePickerService filePickerService,
        ISyncService syncService,
        IQrScannerService? qrScanner = null)
    {
        _configuration = configuration;
        _databaseBackup = databaseBackup;
        _filePickerService = filePickerService;
        _syncService = syncService;
        _qrScanner = qrScanner;
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

        if (Databases.Count > 0 && !Databases.Any(d => d.IsSelected))
        {
            Databases[0].IsSelected = true;
        }

        SelectedDatabase = Databases.FirstOrDefault(d => d.IsSelected) ?? Databases.FirstOrDefault();
        _suppressDepositWarningWrite = true;
        DepositWarningDays = root.GetDepositWarningDays();
        _suppressDepositWarningWrite = false;
        RefreshSyncState();
    }

    public void UpdateSyncStatus()
    {
        RefreshSyncState();
    }

    private void RefreshSyncState()
    {
        OnPropertyChanged(nameof(IsSyncAvailable));
        SyncStatus = IsSyncAvailable ? "S3 синхронизация включена" : "S3 синхронизация отключена";
        SyncNowCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    public async Task AddDatabaseAsync()
    {
        var database = DatabaseViewModel.CreateNew(_filePickerService);
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
        SelectedDatabase = result;
        DeleteDatabaseCommand.NotifyCanExecuteChanged();
        RefreshSyncState();
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    public async Task EditDatabaseAsync()
    {
        if (SelectedDatabase == null)
        {
            return;
        }

        var copy = SelectedDatabase.Clone(_filePickerService);
        var result = await WeakReferenceMessenger.Default.Send(new ModelEditMessage<DatabaseViewModel>(copy));
        if (result == null)
        {
            return;
        }

        SelectedDatabase.ApplyFrom(result);
        Save();
        RefreshSyncState();
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    public async Task ConfigureS3Async()
    {
        if (SelectedDatabase == null)
        {
            return;
        }

        var copy = SelectedDatabase.Clone(_filePickerService);
        copy.InitialTabIndex = 1;
        var result = await WeakReferenceMessenger.Default.Send(new ModelEditMessage<DatabaseViewModel>(copy));
        if (result == null)
        {
            return;
        }

        SelectedDatabase.ApplyFrom(result);
        Save();
        RefreshSyncState();
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

        if (wasSelected)
        {
            Databases[0].IsSelected = true;
        }

        SelectedDatabase = Databases.FirstOrDefault(d => d.IsSelected) ?? Databases.FirstOrDefault();
        Save();
        DeleteDatabaseCommand.NotifyCanExecuteChanged();
        RefreshSyncState();
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
        RefreshSyncState();
        await Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(IsQrImportAvailable))]
    public async Task ImportFromQrAsync()
    {
        if (_qrScanner == null)
        {
            return;
        }

        string? text;
        try
        {
            text = await _qrScanner.ScanAsync();
        }
        catch (System.OperationCanceledException)
        {
            return;
        }
        catch (System.Exception ex)
        {
            SyncStatus = $"Ошибка сканирования: {ex.Message}";
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var payload = DatabaseQrPayload.TryParse(text);
        if (payload == null)
        {
            SyncStatus = "QR-код не содержит настроек приложения";
            return;
        }

        var config = payload.ToConfiguration();
        var database = Databases.FirstOrDefault(d => d.SyncId == config.SyncId);
        if (database == null)
        {
            database = new DatabaseViewModel();
            database.FillFrom(config);
            if (Databases.Count == 0)
            {
                database.IsSelected = true;
            }

            Databases.Add(database);
        }
        else
        {
            var updated = new DatabaseViewModel();
            updated.FillFrom(config);
            database.ApplyFrom(updated);
        }

        Save();
        SelectedDatabase = database;
        DeleteDatabaseCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsSyncAvailable));
        SyncNowCommand.NotifyCanExecuteChanged();
        SyncStatus = $"Настройки получены: {config.Name}";
    }

    [RelayCommand(CanExecute = nameof(CanSyncNow))]
    public async Task SyncNowAsync()
    {
        IsSyncing = true;
        SyncStatus = "Синхронизация...";
        try
        {
            var result = await _syncService.SyncAsync();
            SyncStatus = result.Success ? result.Message : $"Ошибка: {result.Message}";
        }
        finally
        {
            IsSyncing = false;
        }
    }

    private bool CanSyncNow() => IsSyncAvailable && !IsSyncing;

    [RelayCommand(CanExecute = nameof(CanBackupNow))]
    public async Task BackupNowAsync()
    {
        if (SelectedDatabase == null)
        {
            return;
        }

        var configuration = SelectedDatabase.ToConfiguration();
        if (!File.Exists(configuration.GetResolvedPath()))
        {
            BackupStatus = "Файл базы не найден.";
            return;
        }

        var directory = configuration.GetResolvedBackupDirectory();
        Directory.CreateDirectory(directory);
        var destination = await _filePickerService.PickSaveBackupFileAsync(
            directory,
            configuration.GetSuggestedBackupFileName(DateTime.Now));
        if (destination == null)
        {
            return;
        }

        var syncId = configuration.SyncId;
        IsBackingUp = true;
        BackupStatus = "Создание резервной копии...";
        try
        {
            var result = await Task.Run(() => _databaseBackup.BackupToFile(configuration, destination));
            if (SelectedDatabase?.SyncId == syncId)
            {
                BackupStatus = result.Message;
            }
        }
        finally
        {
            IsBackingUp = false;
        }
    }

    private bool CanBackupNow()
    {
        return SelectedDatabase != null && SelectedDatabase.MaxBackups > 0 && !IsBackingUp;
    }

    [RelayCommand(CanExecute = nameof(CanRestoreBackup))]
    public async Task RestoreBackupAsync()
    {
        if (SelectedDatabase == null)
        {
            return;
        }

        var configuration = SelectedDatabase.ToConfiguration();
        var directory = configuration.GetResolvedBackupDirectory();
        Directory.CreateDirectory(directory);
        var backupPath = await _filePickerService.PickBackupFileAsync(directory);
        if (backupPath == null)
        {
            return;
        }

        var syncId = configuration.SyncId;
        var wasActive = SelectedDatabase.IsSelected;
        IsBackingUp = true;
        BackupStatus = "Восстановление базы...";
        try
        {
            var result = await Task.Run(() => _databaseBackup.RestoreFromFile(configuration, backupPath));
            if (SelectedDatabase?.SyncId == syncId)
            {
                BackupStatus = result.Message;
            }

            if (result.Success && wasActive)
            {
                WeakReferenceMessenger.Default.Send(new DatabaseChangedMessage());
            }
        }
        finally
        {
            IsBackingUp = false;
        }
    }

    private bool CanRestoreBackup()
    {
        return SelectedDatabase != null && !IsBackingUp;
    }

    partial void OnSelectedDatabaseChanged(DatabaseViewModel? value)
    {
        if (IsBackingUp)
        {
            return;
        }

        BackupStatus = value?.MaxBackups <= 0
            ? "Резервные копии выключены: максимум равен 0."
            : string.Empty;
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
            _databaseBackup.Backup(root.Databases[previousIndex]);
        }

        var newRoot = new RootConfiguration
        {
            Databases = Databases.Select(d => d.ToConfiguration()).ToArray(),
            SelectedDatabaseIndex = selectedIndex,
            Transactions = root.Transactions,
            AccountsPaneWidth = root.AccountsPaneWidth,
            DepositWarningDays = NormalizeDepositWarningDays(DepositWarningDays),
        };

        _configuration.Save(newRoot);
        BackupNowCommand.NotifyCanExecuteChanged();
        if (!IsBackingUp && SelectedDatabase?.MaxBackups <= 0)
        {
            BackupStatus = "Резервные копии выключены: максимум равен 0.";
        }

        if (selectedIndex != previousIndex)
        {
            WeakReferenceMessenger.Default.Send(new DatabaseChangedMessage());
        }
    }

    partial void OnDepositWarningDaysChanged(decimal value)
    {
        if (_suppressDepositWarningWrite)
        {
            return;
        }

        var days = NormalizeDepositWarningDays(value);
        if (value != days)
        {
            DepositWarningDays = days;
            return;
        }

        var root = _configuration.Get();
        if (root.GetDepositWarningDays() == days)
        {
            return;
        }

        root.DepositWarningDays = days;
        _configuration.Save(root);
        WeakReferenceMessenger.Default.Send(new DepositWarningDaysChangedMessage(days));
    }

    private static int NormalizeDepositWarningDays(decimal value)
    {
        var days = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return Math.Clamp(days, 0, RootConfiguration.MaxDepositWarningDays);
    }
}
