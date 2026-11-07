using System;
using System.Collections.Generic;

namespace FamilyMoney.Analytics;

/// <summary>Суммы за один день и остаток на его конец.</summary>
public readonly record struct DayTotals(
    decimal Balance,
    decimal Income,
    decimal Expense,
    decimal Transfers,
    int Count);

/// <summary>
/// Неизменяемый снимок дневных агрегатов по выбранной области счетов:
/// суммы за день, число операций и остаток на конец дня.
/// <para>
/// Смена видимого окна не ходит в базу — корзины собираются по этому снимку.
/// </para>
/// </summary>
public sealed class DailySeries
{
    private readonly decimal[] _balance;
    private readonly decimal[] _incomePrefix;
    private readonly decimal[] _expensePrefix;
    private readonly decimal[] _transferPrefix;
    private readonly int[] _countPrefix;

    public static DailySeries Empty { get; } = new(DateTime.MinValue, []);

    public DailySeries(DateTime firstDay, IReadOnlyList<DayTotals> days)
    {
        ArgumentNullException.ThrowIfNull(days);

        FirstDay = firstDay.Date;
        Days = days.Count;

        _balance = new decimal[Days];
        _incomePrefix = new decimal[Days + 1];
        _expensePrefix = new decimal[Days + 1];
        _transferPrefix = new decimal[Days + 1];
        _countPrefix = new int[Days + 1];

        for (var i = 0; i < Days; i++)
        {
            var day = days[i];
            _balance[i] = day.Balance;
            _incomePrefix[i + 1] = _incomePrefix[i] + day.Income;
            _expensePrefix[i + 1] = _expensePrefix[i] + day.Expense;
            _transferPrefix[i + 1] = _transferPrefix[i] + day.Transfers;
            _countPrefix[i + 1] = _countPrefix[i] + day.Count;
        }
    }

    /// <summary>Первый день серии (день первой операции по области).</summary>
    public DateTime FirstDay { get; }

    /// <summary>Последний день серии.</summary>
    public DateTime LastDay => Days == 0 ? FirstDay : FirstDay.AddDays(Days - 1);

    public int Days { get; }

    public bool IsEmpty => Days == 0;

    /// <summary>
    /// Остаток на конец дня. До начала серии — 0, после конца — последний известный остаток.
    /// </summary>
    public decimal BalanceAt(DateTime day)
    {
        if (IsEmpty)
        {
            return 0m;
        }

        var date = day.Date;
        if (date < FirstDay)
        {
            return 0m;
        }

        var index = (int)(date - FirstDay).TotalDays;
        return index >= Days ? _balance[Days - 1] : _balance[index];
    }

    /// <summary>Суммы за интервал дат включительно, обрезанные по границам серии.</summary>
    public DayTotals Total(DateTime from, DateTime to)
    {
        if (IsEmpty || to.Date < from.Date)
        {
            return default;
        }

        var start = (int)(from.Date - FirstDay).TotalDays;
        var end = (int)(to.Date - FirstDay).TotalDays + 1;

        start = Math.Clamp(start, 0, Days);
        end = Math.Clamp(end, 0, Days);

        if (end <= start)
        {
            return default;
        }

        return new DayTotals(
            0m,
            _incomePrefix[end] - _incomePrefix[start],
            _expensePrefix[end] - _expensePrefix[start],
            _transferPrefix[end] - _transferPrefix[start],
            _countPrefix[end] - _countPrefix[start]);
    }
}
