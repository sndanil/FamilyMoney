using FamilyMoney.ViewModels;
using System;
using System.Collections.Generic;

namespace FamilyMoney.State;

public sealed class MainState
{
    public required IReadOnlyCollection<AccountViewModel> Accounts { get; set; }

    public Guid? SelectedAccountId { get; set; }

    public DateTime PeriodFrom { get; set; }

    public DateTime PeriodTo { get; set; }
}
