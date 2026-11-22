using FamilyMoney.Analytics;
using FamilyMoney.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FamilyMoney.Tests;

public class TopOperationsBuilderTests
{
    private static readonly DateTime From = new(2026, 1, 1);
    private static readonly DateTime To = new(2026, 12, 31);

    private static readonly Guid Food = Guid.NewGuid();
    private static readonly Guid Grocery = Guid.NewGuid();
    private static readonly Guid Transport = Guid.NewGuid();
    private static readonly Guid Fuel = Guid.NewGuid();

    [Fact]
    public void CountsOnlyIncomesForIncomeReport()
    {
        var account = Guid.NewGuid();

        var result = Build(
            new Transaction[]
            {
                TestData.Income(account, TestData.Day(2026, 3, 1), 1_000m),
                TestData.Expense(account, TestData.Day(2026, 3, 2), 250m),
                TestData.Transfer(account, Guid.NewGuid(), 300m, 300m, TestData.Day(2026, 3, 3)),
            },
            account,
            kind: TopOperationKind.Income);

        Assert.Equal(1_000m, result.Total);
        Assert.Equal(1, result.Transactions);
        Assert.Single(result.Items);
        Assert.Equal(1_000m, result.Items[0].Sum);
    }

    [Fact]
    public void MergesIgnoringCommentsWhenAsked()
    {
        var account = Guid.NewGuid();

        var result = Build(
            new Transaction[]
            {
                TestData.Expense(account, TestData.Day(2026, 7, 1), 100m, Food, Grocery, "пятёрочка"),
                TestData.Expense(account, TestData.Day(2026, 7, 2), 200m, Food, Grocery, "магнит"),
                TestData.Expense(account, TestData.Day(2026, 7, 3), 300m, Food, Transport, "метро"),
            },
            account,
            ignoreComments: true);

        Assert.Equal(2, result.Items.Count);

        var grocery = result.Items[0];
        Assert.Equal(300m, grocery.Sum);
        Assert.Equal(2, grocery.Count);
        Assert.Null(grocery.Comment);
        Assert.Equal(Grocery, grocery.SubCategoryId);

        Assert.Equal(300m, result.Items[1].Sum);
        Assert.Equal(600m, result.Total);
    }

    [Fact]
    public void MergesByCommentsByDefault()
    {
        var account = Guid.NewGuid();

        var result = Build(
            new Transaction[]
            {
                TestData.Expense(account, TestData.Day(2026, 7, 1), 100m, Food, Grocery, "пятёрочка"),
                TestData.Expense(account, TestData.Day(2026, 7, 2), 200m, Food, Grocery, "магнит"),
            },
            account);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal("пятёрочка", result.Items[1].Comment);
    }

    [Fact]
    public void IncomeReport_MergesByCategoryAndComment()
    {
        var account = Guid.NewGuid();
        var salary = Guid.NewGuid();

        var result = Build(
            new Transaction[]
            {
                TestData.Income(account, TestData.Day(2026, 3, 1), 1_000m, "аванс", salary),
                TestData.Income(account, TestData.Day(2026, 3, 15), 1_500m, "Аванс ", salary),
                TestData.Income(account, TestData.Day(2026, 3, 20), 900m, "премия", salary),
            },
            account,
            kind: TopOperationKind.Income);

        Assert.Equal(3_400m, result.Total);
        Assert.Equal(3, result.Transactions);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2_500m, result.Items[0].Sum);
        Assert.Equal(2, result.Items[0].Count);
        Assert.Equal(900m, result.Items[1].Sum);
    }

    [Fact]
    public void CountsOnlyCreditTransactions()
    {
        var account = Guid.NewGuid();

        var result = Build(
            new Transaction[]
            {
                TestData.Income(account, TestData.Day(2026, 3, 1), 1_000m),
                TestData.Expense(account, TestData.Day(2026, 3, 2), 250m),
                TestData.Transfer(account, Guid.NewGuid(), 300m, 300m, TestData.Day(2026, 3, 3)),
            },
            account);

        Assert.Equal(250m, result.Total);
        Assert.Equal(1, result.Transactions);
        Assert.Single(result.Items);
        Assert.Equal(250m, result.Items[0].Sum);
    }

    [Fact]
    public void MergesByCategorySubCategoryAndComment()
    {
        var account = Guid.NewGuid();

        var result = Build(
            new Transaction[]
            {
                TestData.Expense(account, TestData.Day(2026, 2, 1), 100m, Food, Grocery, "пятёрочка"),
                TestData.Expense(account, TestData.Day(2026, 2, 5), 50m, Food, Grocery, "пятёрочка"),
                TestData.Expense(account, TestData.Day(2026, 2, 6), 70m, Food, Grocery, "магнит"),
            },
            account);

        Assert.Equal(2, result.Positions);
        Assert.Equal(150m, result.Items[0].Sum);
        Assert.Equal(2, result.Items[0].Count);
        Assert.Equal(70m, result.Items[1].Sum);
    }

    [Fact]
    public void MergesCommentsIgnoringCaseAndSpaces()
    {
        var account = Guid.NewGuid();

        var result = Build(
            new Transaction[]
            {
                TestData.Expense(account, TestData.Day(2026, 2, 1), 100m, Food, Grocery, "Пятёрочка"),
                TestData.Expense(account, TestData.Day(2026, 2, 2), 50m, Food, Grocery, "  пятёрочка   "),
            },
            account);

        Assert.Single(result.Items);
        Assert.Equal(150m, result.Items[0].Sum);
        Assert.Equal(2, result.Items[0].Count);
    }

    [Fact]
    public void EmptyAndNullComments_AreOneGroup()
    {
        var account = Guid.NewGuid();

        var result = Build(
            new Transaction[]
            {
                TestData.Expense(account, TestData.Day(2026, 2, 1), 100m, Food, Grocery, null),
                TestData.Expense(account, TestData.Day(2026, 2, 2), 50m, Food, Grocery, "   "),
            },
            account);

        Assert.Single(result.Items);
        Assert.Equal(150m, result.Items[0].Sum);
        Assert.Null(result.Items[0].Comment);
    }

    [Fact]
    public void TopCutIsAppliedAfterMerging()
    {
        var account = Guid.NewGuid();
        var transactions = new List<Transaction>();

        for (var i = 0; i < 5; i++)
        {
            transactions.Add(TestData.Expense(account, TestData.Day(2026, 4, 1 + i), 100m, Food, Grocery, "сеть"));
        }

        transactions.Add(TestData.Expense(account, TestData.Day(2026, 4, 10), 500m, Transport, Fuel, "бензин"));

        var result = Build(transactions, account, top: 1);

        Assert.Equal(2, result.Positions);
        Assert.Single(result.Items);
        Assert.Equal(500m, result.Items[0].Sum);
    }

    [Fact]
    public void TotalCountsAllExpenses_NotOnlyTop()
    {
        var account = Guid.NewGuid();

        var result = Build(
            new Transaction[]
            {
                TestData.Expense(account, TestData.Day(2026, 5, 1), 100m, Food, Grocery, "a"),
                TestData.Expense(account, TestData.Day(2026, 5, 2), 50m, Transport, Fuel, "b"),
                TestData.Expense(account, TestData.Day(2026, 5, 3), 30m, null, null, "c"),
            },
            account,
            top: 1);

        Assert.Single(result.Items);
        Assert.Equal(100m, result.Items[0].Sum);
        Assert.Equal(180m, result.Total);
        Assert.Equal(3, result.Transactions);
        Assert.Equal(3, result.Positions);
    }

    [Fact]
    public void FiltersByAccountScope()
    {
        var account = Guid.NewGuid();
        var other = Guid.NewGuid();

        var result = Build(
            new Transaction[]
            {
                TestData.Expense(account, TestData.Day(2026, 6, 1), 100m),
                TestData.Expense(other, TestData.Day(2026, 6, 2), 900m),
            },
            account);

        Assert.Equal(100m, result.Total);
        Assert.Single(result.Items);
    }

    [Fact]
    public void PeriodBoundsAreInclusive()
    {
        var account = Guid.NewGuid();

        var result = Build(
            new Transaction[]
            {
                TestData.Expense(account, From, 10m),
                TestData.Expense(account, To, 20m),
                TestData.Expense(account, From.AddDays(-1), 100m),
                TestData.Expense(account, To.AddDays(1), 200m),
            },
            account);

        Assert.Equal(30m, result.Total);
        Assert.Equal(2, result.Transactions);
    }

    [Fact]
    public void WithoutMerge_EachTransactionIsSeparate()
    {
        var account = Guid.NewGuid();

        var result = Build(
            new Transaction[]
            {
                TestData.Expense(account, TestData.Day(2026, 7, 1), 100m, Food, Grocery, "пятёрочка"),
                TestData.Expense(account, TestData.Day(2026, 7, 2), 50m, Food, Grocery, "пятёрочка"),
            },
            account,
            merge: false);

        Assert.Equal(2, result.Positions);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item => Assert.Equal(1, item.Count));
        Assert.Equal(150m, result.Total);
    }

    [Fact]
    public void OrdersBySumDescending()
    {
        var account = Guid.NewGuid();

        var result = Build(
            new Transaction[]
            {
                TestData.Expense(account, TestData.Day(2026, 8, 1), 50m, Food, Grocery, "a"),
                TestData.Expense(account, TestData.Day(2026, 8, 2), 300m, Transport, Fuel, "b"),
                TestData.Expense(account, TestData.Day(2026, 8, 3), 120m, null, null, "c"),
            },
            account);

        Assert.Equal([300m, 120m, 50m], result.Items.Select(item => item.Sum));
    }

    [Fact]
    public void EmptyResult_WhenNoExpenses()
    {
        var account = Guid.NewGuid();

        var result = Build(
            new Transaction[] { TestData.Income(account, TestData.Day(2026, 9, 1), 100m) },
            account);

        Assert.Empty(result.Items);
        Assert.Equal(0m, result.Total);
        Assert.Equal(0, result.Transactions);
        Assert.Equal(0, result.Positions);
    }

    [Fact]
    public void DeletedTransactionsAreIgnored()
    {
        var account = Guid.NewGuid();
        var expense = TestData.Expense(account, TestData.Day(2026, 10, 1), 100m);
        expense.DeletedAt = TestData.Day(2026, 10, 2);

        var result = Build(new Transaction[] { expense }, account);

        Assert.Empty(result.Items);
        Assert.Equal(0m, result.Total);
    }

    private static TopOperationsResult Build(
        IEnumerable<Transaction> transactions,
        Guid account,
        int top = 10,
        bool merge = true,
        bool ignoreComments = false,
        TopOperationKind kind = TopOperationKind.Expense)
    {
        return TopOperationsBuilder.Build(
            transactions,
            new HashSet<Guid> { account },
            From,
            To,
            top,
            merge,
            ignoreComments,
            kind);
    }
}
