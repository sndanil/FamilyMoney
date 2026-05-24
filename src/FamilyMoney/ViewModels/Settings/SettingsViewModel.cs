using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.DataAccess;
using FamilyMoney.Messages;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace FamilyMoney.ViewModels.Settings;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IRepository _repository;

    public ObservableCollection<DatabaseViewModel> Databases { get; private set; } = [];

    public SettingsViewModel(IRepository repository)
    {
        _repository = repository;
    }

    [RelayCommand]
    public async Task AddDatabaseAsync()
    {
        var database = new DatabaseViewModel();

        var result = await WeakReferenceMessenger.Default.Send(new ModelEditMessage<DatabaseViewModel>(database));
        if (result != null)
        {
            Databases.Add(result);
        }
    }
}
