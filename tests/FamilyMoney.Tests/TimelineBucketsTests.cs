using FamilyMoney.Analytics;
using FamilyMoney.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FamilyMoney.Tests;

public class TimelineBucketsTests
{
    [Fact]
    public void MonthScale_AlignsToCalendarAndCarriesBalance()
    {
        var account = Guid.NewGuid();
        var series = Series(
            account,
            TestData.Income(account, TestData.Day(2024, 12, 20), 1_000m));

        var buckets = TimelineBuckets.Build(series, TestData.Day(2025, 1, 15), TestData.Day(2025, 3, 10), TimelineScale.Months);

        Assert.Equal(3, buckets.Count);
        Assert.Equal(TestData.Day(2025, 1, 1), buckets[0].Start);
        Assert.Equal(TestData.Day(2025, 1, 31), buckets[0].End);
        Assert.Equal(TestData.Day(2025, 2, 28), buckets[1].End);
        Assert.Equal(TestData.Day(2025, 3, 1), buckets[2].Start);
        Assert.Equal(TestData.Day(2025, 3, 31), buckets[2].End);

        Assert.Equal("янв", buckets[0].Label);
        Assert.Equal("январь 2025", buckets[0].Title);

        // Остаток тянется из истории, даже если операций в январе не было.
        Assert.Equal(1_000m, buckets[0].Balance);
        Assert.Equal(1_000m, buckets[0].PreviousBalance);
        Assert.Equal(0m, buckets[0].Change);
        Assert.Equal(0m, buckets[0].Income);
        Assert.Equal(0, buckets[0].Count);
    }

    [Fact]
    public void BalanceAtBucketEnd_DoesNotDependOnWindowStart()
    {
        var account = Guid.NewGuid();
        var series = Series(
            account,
            TestData.Income(account, TestData.Day(2024, 11, 5), 300m),
            TestData.Income(account, TestData.Day(2025, 1, 10), 200m));

        var buckets = TimelineBuckets.Build(series, TestData.Day(2025, 1, 1), TestData.Day(2025, 1, 31), TimelineScale.Months);

        Assert.Single(buckets);
        Assert.Equal(500m, buckets[0].Balance);
        Assert.Equal(300m, buckets[0].PreviousBalance);
        Assert.Equal(200m, buckets[0].Income);
        Assert.Equal(200m, buckets[0].Change);
    }

    [Fact]
    public void DayScale_UsesExactDaysAndSupportsLeapYear()
    {
        var account = Guid.NewGuid();
        var series = Series(
            account,
            TestData.Income(account, TestData.Day(2024, 2, 29), 50m));

        var buckets = TimelineBuckets.Build(series, TestData.Day(2024, 2, 28), TestData.Day(2024, 3, 1), TimelineScale.Days);

        Assert.Equal(3, buckets.Count);
        Assert.Equal(TestData.Day(2024, 2, 29), buckets[1].Start);
        Assert.Equal(TestData.Day(2024, 2, 29), buckets[1].End);
        Assert.Equal("29.02", buckets[1].Label);
        Assert.Equal(50m, buckets[1].Income);
        Assert.Equal(50m, buckets[1].Balance);
    }

    [Fact]
    public void MonthScale_HandlesLeapFebruary()
    {
        var account = Guid.NewGuid();
        var series = Series(account, TestData.Income(account, TestData.Day(2024, 2, 1), 10m));

        var buckets = TimelineBuckets.Build(series, TestData.Day(2024, 2, 1), TestData.Day(2024, 2, 20), TimelineScale.Months);

        Assert.Single(buckets);
        Assert.Equal(TestData.Day(2024, 2, 29), buckets[0].End);
    }

    [Fact]
    public void YearScale_UsesCalendarYears()
    {
        var account = Guid.NewGuid();
        var series = Series(account, TestData.Income(account, TestData.Day(2024, 6, 1), 10m));

        var buckets = TimelineBuckets.Build(series, TestData.Day(2024, 6, 1), TestData.Day(2025, 2, 1), TimelineScale.Years);

        Assert.Equal(2, buckets.Count);
        Assert.Equal("2024", buckets[0].Label);
        Assert.Equal(TestData.Day(2024, 1, 1), buckets[0].Start);
        Assert.Equal(TestData.Day(2024, 12, 31), buckets[0].End);
        Assert.Equal("2025", buckets[1].Label);
    }

    [Fact]
    public void DayScale_ProducesOneBucketPerDay()
    {
        var account = Guid.NewGuid();
        var series = Series(account, TestData.Income(account, TestData.Day(2025, 1, 1), 10m));

        var buckets = TimelineBuckets.Build(series, TestData.Day(2025, 1, 1), TestData.Day(2025, 1, 3), TimelineScale.Days);

        Assert.Equal(3, buckets.Count);
        Assert.Equal(TestData.Day(2025, 1, 1), buckets[0].Start);
        Assert.Equal(TestData.Day(2025, 1, 2), buckets[1].Start);
        Assert.Equal(TestData.Day(2025, 1, 3), buckets[2].Start);
        Assert.Equal(10m, buckets[2].Balance);
    }

    [Fact]
    public void Captions_GroupMonthsByYear()
    {
        var account = Guid.NewGuid();
        var series = Series(account, TestData.Income(account, TestData.Day(2024, 1, 1), 10m));

        var buckets = TimelineBuckets.Build(series, TestData.Day(2024, 11, 1), TestData.Day(2025, 2, 28), TimelineScale.Months);
        var captions = TimelineBuckets.BuildCaptions(buckets, TimelineScale.Months);

        Assert.Equal(2, captions.Count);
        Assert.Equal("2024", captions[0].Text);
        Assert.Equal(buckets[0].Start, captions[0].From);
        Assert.Equal(buckets[1].End, captions[0].To);
        Assert.Equal("2025", captions[1].Text);
    }

    [Fact]
    public void Captions_GroupDaysByMonth()
    {
        var account = Guid.NewGuid();
        var series = Series(account, TestData.Income(account, TestData.Day(2026, 10, 1), 10m));

        var buckets = TimelineBuckets.Build(series, TestData.Day(2026, 10, 29), TestData.Day(2026, 11, 3), TimelineScale.Days);
        var captions = TimelineBuckets.BuildCaptions(buckets, TimelineScale.Days);

        Assert.Equal(2, captions.Count);
        Assert.Equal("октябрь 2026", captions[0].Text);
        Assert.Equal("ноябрь 2026", captions[1].Text);
    }

    [Fact]
    public void YearScale_HasNoCaptions()
    {
        var account = Guid.NewGuid();
        var series = Series(account, TestData.Income(account, TestData.Day(2024, 1, 1), 10m));

        var buckets = TimelineBuckets.Build(series, TestData.Day(2024, 1, 1), TestData.Day(2025, 12, 31), TimelineScale.Years);

        Assert.Empty(TimelineBuckets.BuildCaptions(buckets, TimelineScale.Years));
    }

    [Fact]
    public void Change_AlwaysEqualsIncomeMinusExpense()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var series = Series(
            first,
            second,
            TestData.Income(first, TestData.Day(2024, 3, 1), 1_000m),
            TestData.Expense(first, TestData.Day(2024, 4, 1), 400m),
            // Оба счёта внутри области, суммы разные: разница уходит в расходы.
            TestData.Transfer(first, second, 200m, 150m, TestData.Day(2024, 5, 1)),
            // Перевод пришёл на первый счёт: доход.
            TestData.Transfer(second, first, 300m, 300m, TestData.Day(2024, 6, 1)));

        var buckets = TimelineBuckets.Build(series, TestData.Day(2024, 1, 1), TestData.Day(2024, 12, 31), TimelineScale.Months);

        Assert.All(buckets, bucket => Assert.Equal(bucket.Income - bucket.Expense, bucket.Change));
    }

    private static DailySeries Series(Guid account, params Transaction[] transactions)
    {
        return TimelineSeriesBuilder.Build(transactions, new HashSet<Guid> { account });
    }

    private static DailySeries Series(Guid first, Guid second, params Transaction[] transactions)
    {
        return TimelineSeriesBuilder.Build(transactions, new HashSet<Guid> { first, second });
    }
}
