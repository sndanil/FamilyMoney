using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FamilyMoney.Analytics;

/// <summary>
/// Календарные корзины (отсечки) и правило выбора шага.
/// <para>
/// Корзины выровнены по календарю: год — 1 января…31 декабря, месяц — первое…последнее число.
/// Крайние корзины считаются по полному периоду, обрезается только отрисовка.
/// </para>
/// </summary>
public static class TimelineBuckets
{
    /// <summary>Желаемая ширина одной отсечки на экране.</summary>
    public const double TargetPixelsPerBucket = 28d;

    /// <summary>Меньше трёх отсечек показывать бессмысленно.</summary>
    public const int MinVisibleBuckets = 3;

    /// <summary>Запас, в пределах которого текущий масштаб не меняется (против «мигания»).</summary>
    public const double Hysteresis = 1.2d;

    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public static IReadOnlyList<TimelineBucket> Build(DailySeries series, DateTime from, DateTime to, TimelineScale scale)
    {
        ArgumentNullException.ThrowIfNull(series);

        var result = new List<TimelineBucket>();
        if (to.Date < from.Date)
        {
            return result;
        }

        var start = Align(from.Date, scale);
        while (start.Date <= to.Date)
        {
            var end = EndOf(start, scale);
            var totals = series.Total(start, end);

            result.Add(new TimelineBucket(
                start,
                end,
                Label(start, scale),
                Title(start, scale),
                series.BalanceAt(end),
                series.BalanceAt(start.AddDays(-1)),
                totals.Income,
                totals.Expense,
                totals.Transfers,
                totals.Count));

            if (start.Year >= 9999)
            {
                break;
            }

            start = Next(start, scale);
        }

        return result;
    }

    /// <summary>
    /// Шаг отсечек по ширине графика: сначала пытаемся сохранить текущий,
    /// иначе берём самый мелкий, который ещё читается.
    /// </summary>
    public static TimelineScale ChooseScale(DateTime from, DateTime to, double plotWidth, TimelineScale? current = null)
    {
        var maxBuckets = Math.Max(MinVisibleBuckets, (int)(plotWidth / TargetPixelsPerBucket));

        if (current.HasValue)
        {
            var currentCount = CountBuckets(from, to, current.Value);
            if (currentCount >= MinVisibleBuckets && currentCount <= maxBuckets * Hysteresis)
            {
                return current.Value;
            }
        }

        var days = (to.Date - from.Date).Days + 1;
        if (days <= 2 * maxBuckets)
        {
            return TimelineScale.Days;
        }

        return CountBuckets(from, to, TimelineScale.Months) <= maxBuckets
            ? TimelineScale.Months
            : TimelineScale.Years;
    }

    public static int CountBuckets(DateTime from, DateTime to, TimelineScale scale)
    {
        if (to.Date < from.Date)
        {
            return 0;
        }

        return scale switch
        {
            TimelineScale.Days => (to.Date - from.Date).Days + 1,
            TimelineScale.Months => ((to.Year - from.Year) * 12) + (to.Month - from.Month) + 1,
            _ => to.Year - from.Year + 1,
        };
    }

    public static IReadOnlyList<TimelineCaption> BuildCaptions(IReadOnlyList<TimelineBucket> buckets, TimelineScale scale)
    {
        ArgumentNullException.ThrowIfNull(buckets);

        var result = new List<TimelineCaption>();
        if (buckets.Count == 0)
        {
            return result;
        }

        switch (scale)
        {
            case TimelineScale.Months:
                foreach (var group in buckets.GroupBy(b => b.Start.Year))
                {
                    result.Add(new TimelineCaption(
                        group.First().Start,
                        group.Last().End,
                        group.Key.ToString(Ru)));
                }

                break;

            case TimelineScale.Days:
                foreach (var group in buckets.GroupBy(b => (b.Start.Year, b.Start.Month)))
                {
                    result.Add(new TimelineCaption(
                        group.First().Start,
                        group.Last().End,
                        $"{Ru.DateTimeFormat.MonthNames[group.Key.Month - 1]} {group.Key.Year.ToString(Ru)}"));
                }

                break;
        }

        return result;
    }

    private static DateTime Align(DateTime date, TimelineScale scale) => scale switch
    {
        TimelineScale.Days => date.Date,
        TimelineScale.Months => new DateTime(date.Year, date.Month, 1),
        _ => new DateTime(date.Year, 1, 1),
    };

    private static DateTime Next(DateTime date, TimelineScale scale) => scale switch
    {
        TimelineScale.Days => date.AddDays(1),
        TimelineScale.Months => date.AddMonths(1),
        _ => date.AddYears(1),
    };

    private static DateTime EndOf(DateTime date, TimelineScale scale) => scale switch
    {
        TimelineScale.Days => date.Date,
        TimelineScale.Months => new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)),
        _ => new DateTime(date.Year, 12, 31),
    };

    private static string Label(DateTime start, TimelineScale scale) => scale switch
    {
        TimelineScale.Days => start.ToString("dd.MM", Ru),
        TimelineScale.Months => Ru.DateTimeFormat.AbbreviatedMonthNames[start.Month - 1].TrimEnd('.'),
        _ => start.Year.ToString(Ru),
    };

    private static string Title(DateTime start, TimelineScale scale) => scale switch
    {
        TimelineScale.Days => start.ToString("dd.MM.yyyy", Ru),
        TimelineScale.Months => $"{Ru.DateTimeFormat.MonthNames[start.Month - 1]} {start.Year.ToString(Ru)}",
        _ => start.Year.ToString(Ru),
    };
}
