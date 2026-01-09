using CommunityToolkit.Mvvm.ComponentModel;
using FamilyMoney.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FamilyMoney.ViewModels;

public partial class TransactionsByDatesGroup : ViewModelBase
{
    public bool IsDebet
    {
        get => Sum >= 0;
    }

    [ObservableProperty]
    public partial DateTime Date { get; set; }

    [ObservableProperty]
    public partial decimal Sum { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<TransactionRowViewModel> Transactions { get; set; } = [];

    [ObservableProperty]
    public partial TransactionsViewModel? Parent { get; set; }

    public void SortByLastChange()
    {
        var sortedTransactions = Transactions.OrderBy(t => t.LastChange).ToList();
        Transactions.Clear();
        Transactions.AddRange(sortedTransactions);
    }
}


