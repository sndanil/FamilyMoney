using CommunityToolkit.Mvvm.ComponentModel;
using FamilyMoney.Configuration;

namespace FamilyMoney.ViewModels.Settings;

public partial class FolderStorageViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Path { get; set; } = string.Empty;

    public void FillFrom(FolderStorageConfiguration config)
    {
        Path = config.Path;
    }

    public FolderStorageConfiguration ToConfiguration()
    {
        return new FolderStorageConfiguration
        {
            Path = Path.Trim(),
        };
    }

    public void ApplyFrom(FolderStorageViewModel source)
    {
        Path = source.Path;
    }
}
