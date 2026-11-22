using System;

namespace FamilyMoney.Analytics;

/// <summary>
/// Одна позиция топа: категория, подкатегория, комментарий и сумма.
/// <para>
/// <see cref="Count"/> — сколько операций попало в позицию (при объединении повторов).
/// </para>
/// </summary>
public sealed record TopExpenseGroup(
    Guid? CategoryId,
    Guid? SubCategoryId,
    string? Comment,
    decimal Sum,
    int Count);
