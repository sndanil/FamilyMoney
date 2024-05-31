using FamilyMoney.ViewModels;
using System;
using System.Collections.Generic;

namespace FamilyMoney.State;

public record MainState(IReadOnlyCollection<AccountViewModel> Accounts, Guid? SelectedAccountId, DateTime PeriodFrom, DateTime PeriodTo)
{
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
}
