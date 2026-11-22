using FamilyMoney.Models;
using System;

namespace FamilyMoney.Tests;

/// <summary>Мелкие фабрики моделей, чтобы тесты читались как сценарии.</summary>
internal static class TestData
{
    public static Account Account(Guid id, string name, bool isGroup = false, Guid? parentId = null, bool isNotSummable = false)
    {
        return new Account
        {
            Id = id,
            Name = name,
            IsGroup = isGroup,
            ParentId = parentId,
            IsNotSummable = isNotSummable,
        };
    }

    public static DebetTransaction Income(
        Guid accountId,
        DateTime date,
        decimal sum,
        string? comment = null,
        Guid? categoryId = null,
        Guid? subCategoryId = null)
    {
        return new DebetTransaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Date = date,
            Sum = sum,
            Comment = comment,
            CategoryId = categoryId,
            SubCategoryId = subCategoryId,
        };
    }

    public static CreditTransaction Expense(
        Guid accountId,
        DateTime date,
        decimal sum,
        Guid? categoryId = null,
        Guid? subCategoryId = null,
        string? comment = null)
    {
        return new CreditTransaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Date = date,
            Sum = sum,
            CategoryId = categoryId,
            SubCategoryId = subCategoryId,
            Comment = comment,
        };
    }

    public static TransferTransaction Transfer(Guid fromAccountId, Guid toAccountId, decimal sum, decimal toSum, DateTime date)
    {
        return new TransferTransaction
        {
            Id = Guid.NewGuid(),
            AccountId = fromAccountId,
            ToAccountId = toAccountId,
            Sum = sum,
            ToSum = toSum,
            Date = date,
        };
    }

    public static DateTime Day(int year, int month, int day) => new(year, month, day);
}
