using CommunityToolkit.Mvvm.ComponentModel;

namespace FamilyMoney.ViewModels;

public partial class BaseTransactionsGroupViewModel : ViewModelBase
{
    public bool IsDebet { get; set; }

    public decimal Sum { get; set; }

    public decimal Percent { get; set; }

    public TransactionsViewModel? Parent { get; init; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [ObservableProperty]
    public partial bool IsSelected {  get; set; }
}

