using FamilyMoney.Models;
using FamilyMoney.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyMoney.State;

public record MainState(IReadOnlyList<AccountViewModel> Accounts, Guid? SelectedAccountId, DateTime PeriodFrom, DateTime PeriodTo)
{
    public IReadOnlyList<AccountViewModel> FlatAccounts
    {
        get => Accounts.SelectMany(a => (AccountViewModel[])[a, .. a.Children]).ToList();
    }
}
