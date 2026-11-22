using FamilyMoney.Models;
using FamilyMoney.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyMoney.Analytics;

/// <summary>
/// Собирает топ операций за период по выбранной области счетов — расходов
/// (<see cref="CreditTransaction"/>) или доходов (<see cref="DebetTransaction"/>).
/// <para>
/// Переводы в расчёт не идут: это не трата и не доход. Доля каждой позиции считается
/// от <see cref="TopOperationsResult.Total"/>, то есть от всех операций периода нужного
/// вида, а не от суммы топа.
/// </para>
/// </summary>
public static class TopOperationsBuilder
{
    public static TopOperationsResult Build(
        IEnumerable<Transaction> transactions,
        ISet<Guid> accountIds,
        DateTime from,
        DateTime to,
        int top,
        bool merge,
        TopOperationKind kind)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(accountIds);

        var take = Math.Max(1, top);
        var fromDate = from.Date;
        var toDate = to.Date;

        var total = 0m;
        var operations = 0;

        var items = merge
            ? BuildMerged(transactions, accountIds, fromDate, toDate, ref total, ref operations, kind)
            : BuildSeparate(transactions, accountIds, fromDate, toDate, ref total, ref operations, kind);

        order(items);

        return new TopOperationsResult(items.Take(take).ToList(), total, operations, items.Count);

        static void order(List<TopOperationGroup> list)
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

    private static List<TopOperationGroup> BuildMerged(
        IEnumerable<Transaction> transactions,
        ISet<Guid> accountIds,
        DateTime from,
        DateTime to,
        ref decimal total,
        ref int operations,
        TopOperationKind kind)
    {
        var groups = new Dictionary<GroupKey, Accumulator>();

        foreach (var transaction in transactions)
        {
            if (!Matches(transaction, accountIds, from, to, kind))
            {
                continue;
            }

            total += transaction.Sum;
            operations++;

            var key = new GroupKey(
                transaction.CategoryId,
                transaction.SubCategoryId,
                CommentNormalizer.Normalize(transaction.Comment));

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
            .Select(pair => new TopOperationGroup(
                pair.Key.CategoryId,
                pair.Key.SubCategoryId,
                pair.Value.Comment,
                pair.Value.Sum,
                pair.Value.Count))
            .ToList();
    }

    private static List<TopOperationGroup> BuildSeparate(
        IEnumerable<Transaction> transactions,
        ISet<Guid> accountIds,
        DateTime from,
        DateTime to,
        ref decimal total,
        ref int operations,
        TopOperationKind kind)
    {
        var items = new List<TopOperationGroup>();

        foreach (var transaction in transactions)
        {
            if (!Matches(transaction, accountIds, from, to, kind))
            {
                continue;
            }

            total += transaction.Sum;
            operations++;

            items.Add(new TopOperationGroup(
                transaction.CategoryId,
                transaction.SubCategoryId,
                CleanComment(transaction.Comment),
                transaction.Sum,
                1));
        }

        return items;
    }

    private static bool Matches(
        Transaction transaction,
        ISet<Guid> accountIds,
        DateTime from,
        DateTime to,
        TopOperationKind kind)
    {
        if (!TopOperations.Matches(transaction, kind))
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
