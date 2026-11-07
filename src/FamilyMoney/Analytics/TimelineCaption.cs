namespace FamilyMoney.Analytics;

/// <summary>Крупная подпись оси X: год на масштабе «месяцы», месяц на масштабе «дни».</summary>
public sealed record TimelineCaption(System.DateTime From, System.DateTime To, string Text);
