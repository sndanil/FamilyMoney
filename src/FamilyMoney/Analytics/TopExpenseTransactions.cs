using FamilyMoney.Models;
using FamilyMoney.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyMoney.Analytics;

/// <summary>Операции одного дня выбранной позиции отчёта.</summary>
public sealed record ExpenseDay(DateTime Date, IReadOnlyList<Transaction> Items)
{
    public decimal Sum => Items.Sum(item => item.Sum);
}

/// <summary>
/// Выборка операций, из которых сложилась карточка «Топ расходов»: та же категория,
/// подкатегория и нормализованный комментарий, что и при объединении в отчёте.
/// </summary>
public static class TopExpenseTransactions
{
    public static IReadOnlyList<Transaction> Select(
        IEnumerable<Transaction> transactions,
        Guid? categoryId,
        Guid? subCategoryId,
        string? comment)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        var normalizedComment = CommentNormalizer.Normalize(comment);

        return transactions
            .Where(transaction => IsExpense(transaction)
                && transaction.CategoryId == categoryId
                && transaction.SubCategoryId == subCategoryId
                && CommentNormalizer.Normalize(transaction.Comment) == normalizedComment)
            .ToList();
    }

    /// <summary>Группировка по дням: дни по убыванию, внутри дня — по убыванию суммы.</summary>
    public static IReadOnlyList<ExpenseDay> GroupByDay(IEnumerable<Transaction> transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        return transactions
            .GroupBy(transaction => transaction.Date.Date)
            .OrderByDescending(group => group.Key)
            .Select(group => new ExpenseDay(
                group.Key,
                group.OrderByDescending(transaction => transaction.Sum).ToList()))
            .ToList();
    }

    private static bool IsExpense(Transaction transaction)
    {
        return transaction is CreditTransaction && transaction.DeletedAt == null;
    }
}
