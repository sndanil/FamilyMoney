using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Configuration;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using System.Threading.Tasks;

namespace FamilyMoney.ViewModels.Settings;

public partial class DatabaseViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OkCommand))]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Path { get; set; }

    [ObservableProperty]
    public partial string BackupsFolder { get; set; }

    [ObservableProperty]
    public partial int MaxBackups { get; set; }

    private bool CanOkCommand()
    {
        return !string.IsNullOrEmpty(Name);
    }

    [RelayCommand(CanExecute = nameof(CanOkCommand))]
    public async Task OkAsync()
    {
        WeakReferenceMessenger.Default.Send(new ModelCloseMessage<DatabaseViewModel>(this));
    }

    [RelayCommand]
    public async Task CancelAsync()
    {
        WeakReferenceMessenger.Default.Send(new ModelCloseMessage<DatabaseViewModel>(null));
    }

    public void FillFrom(DatabaseConfiguration config, IRepository repository)
    {
        Name = config.Name;
        Path = config.Path;
        BackupsFolder = config.BackupsFolder;
        MaxBackups = config.MaxBackups;
    }
}

