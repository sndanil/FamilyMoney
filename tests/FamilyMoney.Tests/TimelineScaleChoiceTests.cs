using FamilyMoney.Analytics;
using System;
using Xunit;

namespace FamilyMoney.Tests;

public class TimelineScaleChoiceTests
{
    private const double WidePlot = 860d;

    [Fact]
    public void ThirtyDays_StayOnDayBuckets()
    {
        var from = new DateTime(2025, 1, 1);

        Assert.Equal(TimelineScale.Days, TimelineBuckets.ChooseScale(from, from.AddDays(29), WidePlot));
    }

    [Fact]
    public void SixtyDays_StayOnDayBuckets()
    {
        var from = new DateTime(2024, 1, 1);

        Assert.Equal(TimelineScale.Days, TimelineBuckets.ChooseScale(from, from.AddDays(59), WidePlot));
    }

    [Fact]
    public void SixtyOneDays_SwitchToMonthBuckets()
    {
        var from = new DateTime(2024, 1, 1);

        Assert.Equal(TimelineScale.Months, TimelineBuckets.ChooseScale(from, from.AddDays(60), WidePlot));
    }

    [Fact]
    public void TwoYears_UseMonthBuckets()
    {
        var from = new DateTime(2024, 1, 1);

        Assert.Equal(TimelineScale.Months, TimelineBuckets.ChooseScale(from, new DateTime(2025, 12, 31), WidePlot));
    }

    [Fact]
    public void ThreeYears_UseYearBuckets()
    {
        var from = new DateTime(2024, 1, 1);

        Assert.Equal(TimelineScale.Years, TimelineBuckets.ChooseScale(from, new DateTime(2026, 12, 31), WidePlot));
    }

    [Fact]
    public void NarrowPlot_CoarsensTheScale()
    {
        var from = new DateTime(2024, 1, 1);

        // 24 месяца при ширине 420 px не помещаются: остаётся 15 отсечек.
        Assert.Equal(TimelineScale.Years, TimelineBuckets.ChooseScale(from, new DateTime(2025, 12, 31), 420d));
    }

    [Fact]
    public void Hysteresis_KeepsMonthBucketsWithinTolerance()
    {
        var from = new DateTime(2024, 1, 1);
        var to = new DateTime(2026, 11, 30); // 35 месяцев

        Assert.Equal(TimelineScale.Years, TimelineBuckets.ChooseScale(from, to, WidePlot));
        Assert.Equal(TimelineScale.Months, TimelineBuckets.ChooseScale(from, to, WidePlot, TimelineScale.Months));
    }

    [Fact]
    public void Hysteresis_DoesNotKeepTooManyMonthBuckets()
    {
        var from = new DateTime(2024, 1, 1);
        var to = new DateTime(2027, 12, 31); // 48 месяцев

        Assert.Equal(TimelineScale.Years, TimelineBuckets.ChooseScale(from, to, WidePlot, TimelineScale.Months));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(14, 14)]
    [InlineData(35, 35)]
    public void CountBuckets_CountsCalendarMonths(int months, int expected)
    {
        var from = new DateTime(2024, 1, 1);
        var to = from.AddMonths(months - 1);

        Assert.Equal(expected, TimelineBuckets.CountBuckets(from, to, TimelineScale.Months));
    }

    [Fact]
    public void CountBuckets_CountsDaysAndYears()
    {
        var from = new DateTime(2024, 1, 1);

        Assert.Equal(31, TimelineBuckets.CountBuckets(from, new DateTime(2024, 1, 31), TimelineScale.Days));
        Assert.Equal(2, TimelineBuckets.CountBuckets(from, new DateTime(2025, 12, 31), TimelineScale.Years));
    }
}
