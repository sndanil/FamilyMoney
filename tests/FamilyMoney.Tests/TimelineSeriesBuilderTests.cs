using FamilyMoney.Analytics;
using FamilyMoney.Models;
using System;
using System.Collections.Generic;
using Xunit;

namespace FamilyMoney.Tests;

public class TimelineSeriesBuilderTests
{
    [Fact]
    public void IncomeAndExpense_ProduceDailyBalanceAndSums()
    {
        var account = Guid.NewGuid();
        var series = Build(
            new Transaction[]
            {
                TestData.Income(account, TestData.Day(2025, 1, 1), 100m),
                TestData.Expense(account, TestData.Day(2025, 1, 3), 40m),
            },
            account);

        Assert.Equal(TestData.Day(2025, 1, 1), series.FirstDay);
        Assert.Equal(TestData.Day(2025, 1, 3), series.LastDay);
        Assert.Equal(3, series.Days);

        Assert.Equal(100m, series.BalanceAt(TestData.Day(2025, 1, 1)));
        Assert.Equal(100m, series.BalanceAt(TestData.Day(2025, 1, 2)));
        Assert.Equal(60m, series.BalanceAt(TestData.Day(2025, 1, 3)));

        var totals = series.Total(TestData.Day(2025, 1, 1), TestData.Day(2025, 1, 3));
        Assert.Equal(100m, totals.Income);
        Assert.Equal(40m, totals.Expense);
        Assert.Equal(2, totals.Count);
    }

    [Fact]
    public void BalanceBeforeFirstDay_IsZero()
    {
        var account = Guid.NewGuid();
        var series = Build(new Transaction[] { TestData.Income(account, TestData.Day(2025, 5, 10), 500m) }, account);

        Assert.Equal(0m, series.BalanceAt(TestData.Day(2025, 5, 9)));
        Assert.Equal(500m, series.BalanceAt(TestData.Day(2025, 5, 10)));
    }

    [Fact]
    public void TransferOutOfScope_CountsAsExpense()
    {
        var cash = Guid.NewGuid();
        var card = Guid.NewGuid();

        var series = Build(new Transaction[] { TestData.Transfer(cash, card, 500m, 500m, TestData.Day(2025, 2, 1)) }, cash);

        var totals = series.Total(TestData.Day(2025, 2, 1), TestData.Day(2025, 2, 1));
        Assert.Equal(500m, totals.Expense);
        Assert.Equal(0m, totals.Income);
        Assert.Equal(-500m, totals.Transfers);
        Assert.Equal(-500m, series.BalanceAt(TestData.Day(2025, 2, 1)));
    }

    [Fact]
    public void TransferIntoScope_CountsAsIncome()
    {
        var cash = Guid.NewGuid();
        var card = Guid.NewGuid();

        var series = Build(new Transaction[] { TestData.Transfer(cash, card, 500m, 480m, TestData.Day(2025, 2, 1)) }, card);

        var totals = series.Total(TestData.Day(2025, 2, 1), TestData.Day(2025, 2, 1));
        Assert.Equal(480m, totals.Income);
        Assert.Equal(0m, totals.Expense);
        Assert.Equal(480m, series.BalanceAt(TestData.Day(2025, 2, 1)));
    }

    [Fact]
    public void TransferInsideScope_WithEqualSums_ChangesNothing()
    {
        var cash = Guid.NewGuid();
        var card = Guid.NewGuid();

        var series = Build(
            new Transaction[] { TestData.Transfer(cash, card, 500m, 500m, TestData.Day(2025, 2, 1)) },
            cash,
            card);

        var totals = series.Total(TestData.Day(2025, 2, 1), TestData.Day(2025, 2, 1));
        Assert.Equal(0m, totals.Income);
        Assert.Equal(0m, totals.Expense);
        Assert.Equal(0m, totals.Transfers);
        Assert.Equal(0m, series.BalanceAt(TestData.Day(2025, 2, 1)));
        Assert.Equal(1, totals.Count);
    }

    [Fact]
    public void TransferInsideScope_WithDifferentSums_CountsTheDifference()
    {
        var cash = Guid.NewGuid();
        var card = Guid.NewGuid();

        var series = Build(
            new Transaction[] { TestData.Transfer(cash, card, 1_000m, 900m, TestData.Day(2025, 2, 1)) },
            cash,
            card);

        var totals = series.Total(TestData.Day(2025, 2, 1), TestData.Day(2025, 2, 1));
        Assert.Equal(100m, totals.Expense);
        Assert.Equal(0m, totals.Income);
        Assert.Equal(-100m, series.BalanceAt(TestData.Day(2025, 2, 1)));
    }

    [Fact]
    public void TransferOutsideScope_IsIgnored()
    {
        var cash = Guid.NewGuid();
        var card = Guid.NewGuid();
        var wallet = Guid.NewGuid();

        var series = Build(
            new Transaction[] { TestData.Transfer(cash, card, 500m, 500m, TestData.Day(2025, 2, 1)) },
            wallet);

        Assert.True(series.IsEmpty);
    }

    [Fact]
    public void DeletedTransactions_AreIgnored()
    {
        var account = Guid.NewGuid();
        var income = TestData.Income(account, TestData.Day(2025, 1, 1), 100m);
        income.DeletedAt = TestData.Day(2025, 1, 2);

        var series = Build(new Transaction[] { income }, account);

        Assert.True(series.IsEmpty);
    }

    [Fact]
    public void EmptyAccountSet_ProducesEmptySeries()
    {
        var series = TimelineSeriesBuilder.Build([], new HashSet<Guid>());

        Assert.True(series.IsEmpty);
        Assert.Equal(0m, series.BalanceAt(TestData.Day(2025, 1, 1)));
    }

    [Fact]
    public void Total_IsClippedToSeriesRange()
    {
        var account = Guid.NewGuid();
        var series = Build(new Transaction[] { TestData.Income(account, TestData.Day(2025, 1, 10), 100m) }, account);

        var totals = series.Total(TestData.Day(2020, 1, 1), TestData.Day(2030, 1, 1));
        Assert.Equal(100m, totals.Income);

        var outside = series.Total(TestData.Day(2030, 1, 1), TestData.Day(2030, 2, 1));
        Assert.Equal(0m, outside.Income);
    }

    private static DailySeries Build(IEnumerable<Transaction> transactions, params Guid[] accountIds)
    {
        return TimelineSeriesBuilder.Build(transactions, new HashSet<Guid>(accountIds));
    }
}
