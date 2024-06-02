using ReactiveUI;

namespace FamilyMoney.ViewModels;

public class BaseTransactionsGroupViewModel : ViewModelBase
{
    private bool _isExpanded = false;
    private bool _isSelected = false;

    public bool IsDebet { get; set; }

    public decimal Sum { get; set; }

    public decimal Percent { get; set; }

    public TransactionsViewModel? Parent { get; init; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }
}

