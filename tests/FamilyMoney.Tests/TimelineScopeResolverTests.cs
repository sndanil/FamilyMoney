using FamilyMoney.Analytics;
using FamilyMoney.Models;
using System;
using System.Linq;
using Xunit;

namespace FamilyMoney.Tests;

public class TimelineScopeResolverTests
{
    [Fact]
    public void TotalScope_SkipsNotSummableAccounts()
    {
        var group = Guid.NewGuid();
        var regular = Guid.NewGuid();
        var excluded = Guid.NewGuid();

        var accounts = new[]
        {
            TestData.Account(group, "Группа", isGroup: true),
            TestData.Account(regular, "Обычный", parentId: group),
            TestData.Account(excluded, "Не суммируется", parentId: group, isNotSummable: true),
        };

        var ids = TimelineScopeResolver.Resolve(accounts, null);

        Assert.Equal(new[] { regular }, ids.OrderBy(id => id));
    }

    [Fact]
    public void GroupScope_WalksAllLevels()
    {
        var root = Guid.NewGuid();
        var nested = Guid.NewGuid();
        var leaf = Guid.NewGuid();

        var accounts = new[]
        {
            TestData.Account(root, "Корень", isGroup: true),
            TestData.Account(nested, "Вложенная группа", isGroup: true, parentId: root),
            TestData.Account(leaf, "Счёт", parentId: nested),
        };

        Assert.Equal(new[] { leaf }, TimelineScopeResolver.Resolve(accounts, root));
        Assert.Equal(new[] { leaf }, TimelineScopeResolver.Resolve(accounts, nested));
        Assert.Equal(new[] { leaf }, TimelineScopeResolver.Resolve(accounts, null));
    }

    [Fact]
    public void NotSummableGroup_ExcludesItsSubtree()
    {
        var root = Guid.NewGuid();
        var excludedGroup = Guid.NewGuid();
        var insideExcluded = Guid.NewGuid();
        var regular = Guid.NewGuid();

        var accounts = new[]
        {
            TestData.Account(root, "Корень", isGroup: true),
            TestData.Account(excludedGroup, "Скрытая группа", isGroup: true, parentId: root, isNotSummable: true),
            TestData.Account(insideExcluded, "Внутри скрытой", parentId: excludedGroup),
            TestData.Account(regular, "Обычный", parentId: root),
        };

        Assert.Equal(new[] { regular }, TimelineScopeResolver.Resolve(accounts, null));

        // Выбранная явно скрытая группа считается как обычно.
        Assert.Equal(new[] { insideExcluded }, TimelineScopeResolver.Resolve(accounts, excludedGroup));
    }

    [Fact]
    public void NotSummableLeaf_StillCountsWhenSelected()
    {
        var group = Guid.NewGuid();
        var leaf = Guid.NewGuid();

        var accounts = new[]
        {
            TestData.Account(group, "Группа", isGroup: true),
            TestData.Account(leaf, "Копилка", parentId: group, isNotSummable: true),
        };

        Assert.Equal(new[] { leaf }, TimelineScopeResolver.Resolve(accounts, leaf));
    }

    [Fact]
    public void DeletedAccounts_AreSkipped()
    {
        var group = Guid.NewGuid();
        var deleted = Guid.NewGuid();
        var alive = Guid.NewGuid();

        var deletedAccount = TestData.Account(deleted, "Удалённый", parentId: group);
        deletedAccount.DeletedAt = TestData.Day(2025, 1, 1);

        var accounts = new[]
        {
            TestData.Account(group, "Группа", isGroup: true),
            deletedAccount,
            TestData.Account(alive, "Живой", parentId: group),
        };

        Assert.Equal(new[] { alive }, TimelineScopeResolver.Resolve(accounts, null));
    }

    [Fact]
    public void UnknownAccount_ProducesEmptyScope()
    {
        var accounts = new[] { TestData.Account(Guid.NewGuid(), "Счёт") };

        Assert.Empty(TimelineScopeResolver.Resolve(accounts, Guid.NewGuid()));
    }
}
