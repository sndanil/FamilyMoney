using DynamicData;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FamilyMoney.ViewModels;

public sealed class TransactionsByDatesGroup : ViewModelBase
{
    private DateTime _date;
    private decimal _sum = 0;
    private ObservableCollection<TransactionRowViewModel> _transactions = [];
    private TransactionsViewModel? _parent;

    public bool IsDebet
    {
        get => Sum >= 0;
    }

    public DateTime Date
    {
        get => _date;
        set => this.RaiseAndSetIfChanged(ref _date, value);
    }

    public decimal Sum
    {
        get => _sum;
        set => this.RaiseAndSetIfChanged(ref _sum, value);
    }

    public ObservableCollection<TransactionRowViewModel> Transactions
    {
        get => _transactions;
        set => this.RaiseAndSetIfChanged(ref _transactions, value);
    }
    public TransactionsViewModel? Parent
    {
        get => _parent;
        set => this.RaiseAndSetIfChanged(ref _parent, value);
    }

    public void SortByLastChange()
    {
        var sortedTransactions = Transactions.OrderBy(t => t.LastChange).ToList();
        Transactions.Clear();
        Transactions.AddRange(sortedTransactions);
    }
}


