using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FamilyMoney.DataAccess;

public sealed class TransactionsFilter
{
    public Guid? AccountId { get; set; }

    public DateTime PeriodFrom { get; init; }

    public DateTime PeriodTo { get; init; }
}
