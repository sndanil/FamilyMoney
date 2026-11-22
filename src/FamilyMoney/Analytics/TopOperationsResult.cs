using System.Collections.Generic;

namespace FamilyMoney.Analytics;

/// <summary>
/// Результат сборки топа расходов.
/// <para>
/// <see cref="Total"/> — все расходы за период по области (знаменатель для доли),
/// <see cref="Positions"/> — сколько позиций было до отсечения по размеру топа.
/// </para>
/// </summary>
public sealed record TopOperationsResult(
    IReadOnlyList<TopOperationGroup> Items,
    decimal Total,
    int Transactions,
    int Positions);
