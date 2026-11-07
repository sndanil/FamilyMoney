using FamilyMoney.Models;
using System;
using System.Collections.Generic;

namespace FamilyMoney.Analytics;

/// <summary>
/// Собирает <see cref="DailySeries"/> из операций по заданному набору счетов.
/// <para>
/// Перевод раскладывается по чистому изменению баланса области:
/// <c>delta = (ToSum, если получатель внутри области) − (Sum, если отправитель внутри области)</c>.
/// Перевод, который сам себя нивелировал (оба счёта внутри, суммы равны), не попадает
/// ни в доходы, ни в расходы.
/// </para>
/// </summary>
public static class TimelineSeriesBuilder
{
    public static DailySeries Build(IEnumerable<Transaction> transactions, ISet<Guid> accountIds)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(accountIds);

        if (accountIds.Count == 0)
        {
            return DailySeries.Empty;
        }

        var changes = new List<(DateTime Date, Change Change)>();
        DateTime? first = null;
        DateTime? last = null;

        foreach (var transaction in transactions)
        {
            if (transaction.DeletedAt != null || !TryGetChange(transaction, accountIds, out var change))
            {
                continue;
            }

            var date = transaction.Date.Date;
            changes.Add((date, change));
            if (first == null || date < first)
            {
                first = date;
            }

            if (last == null || date > last)
            {
                last = date;
            }
        }

        if (first == null || last == null)
        {
            return DailySeries.Empty;
        }

        var days = (last.Value - first.Value).Days + 1;
        var balance = new decimal[days];
        var income = new decimal[days];
        var expense = new decimal[days];
        var transfers = new decimal[days];
        var counts = new int[days];

        foreach (var (date, change) in changes)
        {
            var index = (int)(date - first.Value).TotalDays;
            balance[index] += change.Delta;
            income[index] += change.Income;
            expense[index] += change.Expense;
            transfers[index] += change.Transfer;
            counts[index]++;
        }

        for (var i = 1; i < days; i++)
        {
            balance[i] += balance[i - 1];
        }

        var totals = new DayTotals[days];
        for (var i = 0; i < days; i++)
        {
            totals[i] = new DayTotals(balance[i], income[i], expense[i], transfers[i], counts[i]);
        }

        return new DailySeries(first.Value, totals);
    }

    private readonly record struct Change(decimal Delta, decimal Income, decimal Expense, decimal Transfer);

    private static bool TryGetChange(Transaction transaction, ISet<Guid> accountIds, out Change change)
    {
        change = default;

        if (transaction is DebetTransaction)
        {
            if (transaction.AccountId is not { } debetAccountId || !accountIds.Contains(debetAccountId))
            {
                return false;
            }

            change = new Change(transaction.Sum, transaction.Sum, 0m, 0m);
            return true;
        }

        if (transaction is CreditTransaction)
        {
            if (transaction.AccountId is not { } creditAccountId || !accountIds.Contains(creditAccountId))
            {
                return false;
            }

            change = new Change(-transaction.Sum, 0m, transaction.Sum, 0m);
            return true;
        }

        if (transaction is TransferTransaction transfer)
        {
            var incoming = transfer.ToAccountId is { } target && accountIds.Contains(target) ? transfer.ToSum : 0m;
            var outgoing = transfer.AccountId is { } source && accountIds.Contains(source) ? transfer.Sum : 0m;

            if (incoming == 0m && outgoing == 0m)
            {
                // Операция не касается выбранной области.
                return false;
            }

            var delta = incoming - outgoing;
            change = new Change(
                delta,
                delta > 0m ? delta : 0m,
                delta < 0m ? -delta : 0m,
                delta);
            return true;
        }

        return false;
    }
}
