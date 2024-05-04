using System;

namespace FamilyMoney.State;

public sealed class MainState
{
    public Guid? SelectedAccountId { get; set; }

    public DateTime PeriodFrom { get; set; }

    public DateTime PeriodTo { get; set; }
}
