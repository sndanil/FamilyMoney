using FamilyMoney.ViewModels;
using System;
using System.Collections.Generic;

namespace FamilyMoney.State;

public sealed class MainState
{
    public required IReadOnlyCollection<AccountViewModel> Accounts { get; set; }

    public IList<AccountViewModel> FlatAccounts
    {
        get
        {
            var result = new List<AccountViewModel>();
            foreach (var account in Accounts)
            {
                result.Add(account);
                foreach (var childAccount in account.Children)
                {
                    result.Add(childAccount);
                }
            }

            return result;
        }
    }

    public Guid? SelectedAccountId { get; set; }

    public DateTime PeriodFrom { get; set; }

    public DateTime PeriodTo { get; set; }
}
