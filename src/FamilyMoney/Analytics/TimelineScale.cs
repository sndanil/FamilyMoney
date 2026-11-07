namespace FamilyMoney.Analytics;

/// <summary>
/// Шаг отсечек на таймлайне. Мельче дня не идём: у операции в базе только дата.
/// </summary>
public enum TimelineScale
{
    Days,
    Months,
    Years,
}
