using FamilyMoney.Analytics;
using FamilyMoney.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FamilyMoney.Tests;

public class TopOperationsTests
{
    private static readonly Guid Food = Guid.NewGuid();
    private static readonly Guid Grocery = Guid.NewGuid();

    [Fact]
    public void Select_KeepsOnlyTheCardPosition()
    {
        var account = Guid.NewGuid();

        var transactions = new List<Transaction>
        {
            TestData.Expense(account, TestData.Day(2026, 1, 2), 100m, Food, Grocery, "пятёрочка"),
            TestData.Expense(account, TestData.Day(2026, 1, 3), 200m, Food, Grocery, "магнит"),
        };

        var selected = TopOperations.Select(transactions, Food, Grocery, "Пятёрочка", TopOperationKind.Expense);

        Assert.Single(selected);
        Assert.Equal(100m, selected[0].Sum);
    }

    [Fact]
    public void Select_NormalizesTheComment()
    {
        var account = Guid.NewGuid();

        var transactions = new List<Transaction>
        {
            TestData.Expense(account, TestData.Day(2026, 2, 2), 100m, Food, Grocery, "  Пятёрочка   центр "),
            TestData.Expense(account, TestData.Day(2026, 2, 3), 200m, Food, Grocery, "Пятёрочка центр"),
        };

        var selected = TopOperations.Select(transactions, Food, Grocery, "пятёрочка центр", TopOperationKind.Expense);

        Assert.Equal(2, selected.Count);
        Assert.Equal(300m, selected.Sum(item => item.Sum));
    }

    [Fact]
    public void Select_TreatsEmptyCategorySubCategoryAndCommentAsConditions()
    {
        var account = Guid.NewGuid();

        var transactions = new List<Transaction>
        {
            TestData.Expense(account, TestData.Day(2026, 3, 1), 100m, null, null, null),
            TestData.Expense(account, TestData.Day(2026, 3, 2), 200m, null, null, "   "),
            TestData.Expense(account, TestData.Day(2026, 3, 3), 300m, Food, Grocery, null),
        };

        var withoutCategory = TopOperations.Select(transactions, null, null, null, TopOperationKind.Expense);
        Assert.Equal(2, withoutCategory.Count);

        var withCategory = TopOperations.Select(transactions, Food, Grocery, null, TopOperationKind.Expense);
        Assert.Single(withCategory);
        Assert.Equal(300m, withCategory[0].Sum);
    }

    [Fact]
    public void Select_IgnoresIncomeTransfersAndDeleted()
    {
        var account = Guid.NewGuid();
        var deleted = TestData.Expense(account, TestData.Day(2026, 4, 1), 500m, Food, Grocery, "пятёрочка");
        deleted.DeletedAt = TestData.Day(2026, 4, 2);

        var transactions = new List<Transaction>
        {
            TestData.Income(account, TestData.Day(2026, 4, 3), 1_000m),
            TestData.Transfer(account, Guid.NewGuid(), 700m, 700m, TestData.Day(2026, 4, 4)),
            deleted,
            TestData.Expense(account, TestData.Day(2026, 4, 5), 100m, Food, Grocery, "пятёрочка"),
        };

        var selected = TopOperations.Select(transactions, Food, Grocery, "пятёрочка", TopOperationKind.Expense);

        Assert.Single(selected);
        Assert.Equal(100m, selected[0].Sum);
    }

    [Fact]
    public void Select_WithIncomeKind_KeepsOnlyIncomes()
    {
        var account = Guid.NewGuid();
        var salary = Guid.NewGuid();

        var transactions = new List<Transaction>
        {
            TestData.Income(account, TestData.Day(2026, 4, 1), 1_000m, "аванс", salary),
            TestData.Income(account, TestData.Day(2026, 4, 2), 500m, "премия", salary),
            TestData.Expense(account, TestData.Day(2026, 4, 3), 300m, salary, null, "аванс"),
        };

        var selected = TopOperations.Select(transactions, salary, null, "аванс", TopOperationKind.Income);

        Assert.Single(selected);
        Assert.Equal(1_000m, selected[0].Sum);
    }

    [Fact]
    public void GroupByDay_OrdersDaysAndItemsDescending()
    {
        var account = Guid.NewGuid();

        var items = new List<Transaction>
        {
            TestData.Expense(account, TestData.Day(2026, 5, 1), 100m),
            TestData.Expense(account, TestData.Day(2026, 5, 2), 300m),
            TestData.Expense(account, TestData.Day(2026, 5, 2), 500m),
            TestData.Expense(account, TestData.Day(2026, 5, 1), 50m),
        };

        var days = TopOperations.GroupByDay(items);

        Assert.Equal(2, days.Count);
        Assert.Equal(TestData.Day(2026, 5, 2), days[0].Date);
        Assert.Equal(800m, days[0].Sum);
        Assert.Equal(500m, days[0].Items[0].Sum);
        Assert.Equal(300m, days[0].Items[1].Sum);

        Assert.Equal(TestData.Day(2026, 5, 1), days[1].Date);
        Assert.Equal(150m, days[1].Sum);
        Assert.Equal(100m, days[1].Items[0].Sum);
    }

    [Fact]
    public void GroupByDay_Empty_ReturnsNothing()
    {
        Assert.Empty(TopOperations.GroupByDay([]));
    }
}
