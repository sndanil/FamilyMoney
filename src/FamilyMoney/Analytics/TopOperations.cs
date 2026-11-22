using FamilyMoney.Models;
using FamilyMoney.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyMoney.Analytics;

/// <summary>Какие операции собирает отчёт «Топ»: расходы или доходы.</summary>
public enum TopOperationKind
{
    Expense,
    Income,
}

/// <summary>Операции одного дня выбранной позиции отчёта.</summary>
public sealed record OperationDay(DateTime Date, IReadOnlyList<Transaction> Items)
{
    public decimal Sum => Items.Sum(item => item.Sum);
}

/// <summary>
/// Выборка операций, из которых сложилась карточка топа: та же категория,
/// подкатегория и нормализованный комментарий, что и при объединении в отчёте.
/// </summary>
public static class TopOperations
{
    public static IReadOnlyList<Transaction> Select(
        IEnumerable<Transaction> transactions,
        Guid? categoryId,
        Guid? subCategoryId,
        string? comment,
        TopOperationKind kind)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        var normalizedComment = CommentNormalizer.Normalize(comment);

        return transactions
            .Where(transaction => Matches(transaction, kind)
                && transaction.CategoryId == categoryId
                && transaction.SubCategoryId == subCategoryId
                && CommentNormalizer.Normalize(transaction.Comment) == normalizedComment)
            .ToList();
    }

    /// <summary>Группировка по дням: дни по убыванию, внутри дня — по убыванию суммы.</summary>
    public static IReadOnlyList<OperationDay> GroupByDay(IEnumerable<Transaction> transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        return transactions
            .GroupBy(transaction => transaction.Date.Date)
            .OrderByDescending(group => group.Key)
            .Select(group => new OperationDay(
                group.Key,
                group.OrderByDescending(transaction => transaction.Sum).ToList()))
            .ToList();
    }

    /// <summary>Операция нужного вида и не удалённая.</summary>
    public static bool Matches(Transaction transaction, TopOperationKind kind)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (transaction.DeletedAt != null)
        {
            return false;
        }

        return kind == TopOperationKind.Expense
            ? transaction is CreditTransaction
            : transaction is DebetTransaction;
    }
}
