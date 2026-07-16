using CommunityToolkit.Mvvm.ComponentModel;

namespace FamilyMoney.ViewModels;

public partial class FilterTagViewModel : ObservableObject
{
    public required string Name { get; init; }

    [ObservableProperty]
    public partial bool IsChecked { get; set; }
}
