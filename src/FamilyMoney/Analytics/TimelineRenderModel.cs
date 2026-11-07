using System;
using System.Collections.Generic;

namespace FamilyMoney.Analytics;

/// <summary>
/// Всё, что нужно контролу для одной отрисовки: корзины, подписи, шкалы и окно.
/// Собирается моделью представления и заменяется целиком при любом изменении.
/// </summary>
public sealed record TimelineRenderModel(
    IReadOnlyList<TimelineBucket> Buckets,
    IReadOnlyList<TimelineCaption> Captions,
    TimelineScale Scale,
    DateTime From,
    DateTime To,
    decimal BalanceFloor,
    decimal BalanceCeiling,
    IReadOnlyList<decimal> BalanceTicks,
    decimal FlowMax,
    IReadOnlyList<decimal> FlowTicks,
    DateTime? HighlightFrom,
    DateTime? HighlightTo);
