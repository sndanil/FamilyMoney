using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;

namespace FamilyMoney.ViewModels;

/// <summary>День в панели операций выбранной карточки: заголовок с датой и операции.</summary>
public partial class ExpenseDayGroupViewModel : ViewModelBase
{
    public required DateTime Date { get; init; }

    public string DateText => Date.ToString("dd MMMM yyyy");

    public decimal Sum { get; init; }

    public IReadOnlyList<ExpenseRowViewModel> Items { get; init; } = [];

    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;
}
