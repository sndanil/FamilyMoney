using System;

namespace FamilyMoney.Analytics;

/// <summary>
/// Одна отсечка таймлайна: календарный интервал и суммы за него.
/// <para>
/// <see cref="Income"/> и <see cref="Expense"/> уже включают вклад переводов, разложенный
/// по чистому изменению баланса выбранной области (см. <see cref="TimelineSeriesBuilder"/>).
/// </para>
/// </summary>
public sealed record TimelineBucket(
    DateTime Start,
    DateTime End,
    string Label,
    string Title,
    decimal Balance,
    decimal PreviousBalance,
    decimal Income,
    decimal Expense,
    decimal Transfers,
    int Count)
{
    /// <summary>Изменение остатка за отсечку — с учётом переводов.</summary>
    public decimal Change => Balance - PreviousBalance;
}
