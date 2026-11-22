using FamilyMoney.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FamilyMoney.Analytics;

/// <summary>
/// Собирает топ расходов за период по выбранной области счетов.
/// <para>
/// В расчёт идут только операции «расход» (<see cref="CreditTransaction"/>): переводы —
/// не траты. Доля каждой позиции считается от <see cref="TopExpensesResult.Total"/>,
/// то есть от всех расходов периода, а не от суммы топа.
/// </para>
/// </summary>
public static class TopExpensesBuilder
{
    public static TopExpensesResult Build(
        IEnumerable<Transaction> transactions,
        ISet<Guid> accountIds,
        DateTime from,
        DateTime to,
        int top,
        bool merge)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(accountIds);

        var take = Math.Max(1, top);
        var fromDate = from.Date;
        var toDate = to.Date;

        var total = 0m;
        var operations = 0;

        var items = merge
            ? BuildMerged(transactions, accountIds, fromDate, toDate, ref total, ref operations)
            : BuildSeparate(transactions, accountIds, fromDate, toDate, ref total, ref operations);

        order(items);

        return new TopExpensesResult(items.Take(take).ToList(), total, operations, items.Count);

        static void order(List<TopExpenseGroup> list)
        {
            list.Sort(static (left, right) =>
            {
                var result = right.Sum.CompareTo(left.Sum);
                if (result != 0)
                {
                    return result;
                }

                result = right.Count.CompareTo(left.Count);
                if (result != 0)
                {
                    return result;
                }

                result = string.CompareOrdinal(left.Comment, right.Comment);
                if (result != 0)
                {
                    return result;
                }

                result = Nullable.Compare(left.CategoryId, right.CategoryId);
                return result != 0 ? result : Nullable.Compare(left.SubCategoryId, right.SubCategoryId);
            });
        }
    }

    private static List<TopExpenseGroup> BuildMerged(
        IEnumerable<Transaction> transactions,
        ISet<Guid> accountIds,
        DateTime from,
        DateTime to,
        ref decimal total,
        ref int operations)
    {
        var groups = new Dictionary<GroupKey, Accumulator>();

        foreach (var transaction in transactions)
        {
            if (!IsExpense(transaction, accountIds, from, to))
            {
                continue;
            }

            total += transaction.Sum;
            operations++;

            var key = new GroupKey(
                transaction.CategoryId,
                transaction.SubCategoryId,
                NormalizeComment(transaction.Comment));

            if (groups.TryGetValue(key, out var accumulator))
            {
                groups[key] = accumulator.Add(transaction.Sum, transaction.Comment);
            }
            else
            {
                groups[key] = new Accumulator(transaction.Sum, 1, CleanComment(transaction.Comment));
            }
        }

        return groups
            .Select(pair => new TopExpenseGroup(
                pair.Key.CategoryId,
                pair.Key.SubCategoryId,
                pair.Value.Comment,
                pair.Value.Sum,
                pair.Value.Count))
            .ToList();
    }

    private static List<TopExpenseGroup> BuildSeparate(
        IEnumerable<Transaction> transactions,
        ISet<Guid> accountIds,
        DateTime from,
        DateTime to,
        ref decimal total,
        ref int operations)
    {
        var items = new List<TopExpenseGroup>();

        foreach (var transaction in transactions)
        {
            if (!IsExpense(transaction, accountIds, from, to))
            {
                continue;
            }

            total += transaction.Sum;
            operations++;

            items.Add(new TopExpenseGroup(
                transaction.CategoryId,
                transaction.SubCategoryId,
                CleanComment(transaction.Comment),
                transaction.Sum,
                1));
        }

        return items;
    }

    private static bool IsExpense(Transaction transaction, ISet<Guid> accountIds, DateTime from, DateTime to)
    {
        if (transaction is not CreditTransaction || transaction.DeletedAt != null)
        {
            return false;
        }

        if (transaction.AccountId is not { } accountId || !accountIds.Contains(accountId))
        {
            return false;
        }

        var date = transaction.Date.Date;
        return date >= from && date <= to;
    }

    /// <summary>
    /// Ключ объединения: комментарий сравнивается без учёта регистра и лишних пробелов,
    /// пустой и null — одно и то же.
    /// </summary>
    private static string NormalizeComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(comment.Length);
        var pendingSpace = false;

        foreach (var character in comment.Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = true;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private static string? CleanComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return null;
        }

        return comment.Trim();
    }

    private readonly record struct GroupKey(Guid? CategoryId, Guid? SubCategoryId, string Comment);

    private readonly record struct Accumulator(decimal Sum, int Count, string? Comment)
    {
        public Accumulator Add(decimal sum, string? comment)
        {
            return this with
            {
                Sum = Sum + sum,
                Count = Count + 1,
                Comment = Comment ?? CleanComment(comment),
            };
        }
    }
}
