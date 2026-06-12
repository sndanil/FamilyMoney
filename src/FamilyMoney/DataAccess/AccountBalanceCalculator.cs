using FamilyMoney.Models;

namespace FamilyMoney.DataAccess;

internal static class AccountBalanceCalculator
{
    public static Dictionary<Guid, decimal> Calculate(IEnumerable<Account> accounts, IEnumerable<Transaction> transactions)
    {
        var sums = accounts
            .Where(a => !a.IsGroup)
            .ToDictionary(a => a.Id, _ => 0m);

        foreach (var transaction in transactions)
        {
            if (transaction.AccountId == null)
            {
                continue;
            }

            if (sums.TryGetValue(transaction.AccountId.Value, out var fromSum))
            {
                sums[transaction.AccountId.Value] = fromSum + GetDeltaFromAccount(transaction);
            }

            if (transaction is TransferTransaction transfer
                && transfer.ToAccountId != null
                && sums.TryGetValue(transfer.ToAccountId.Value, out var toSum))
            {
                sums[transfer.ToAccountId.Value] = toSum + transfer.ToSum;
            }
        }

        return sums;
    }

    private static decimal GetDeltaFromAccount(Transaction transaction) => transaction switch
    {
        DebetTransaction => transaction.Sum,
        CreditTransaction => -transaction.Sum,
        TransferTransaction => -transaction.Sum,
        _ => 0m,
    };
}
