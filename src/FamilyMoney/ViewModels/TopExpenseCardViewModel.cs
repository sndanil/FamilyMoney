namespace FamilyMoney.ViewModels;

/// <summary>
/// Данные одной карточки «Топ расходов». Список карточек заменяется целиком при пересчёте,
/// поэтому отдельная наблюдаемость полям не нужна.
/// </summary>
public sealed class TopExpenseCardViewModel
{
    public required string CategoryName { get; init; }

    public string SubCategoryName { get; init; } = string.Empty;

    public string Comment { get; init; } = string.Empty;

    public decimal Sum { get; init; }

    /// <summary>Доля от всех расходов за период, 0..1.</summary>
    public decimal Percent { get; init; }

    public int Count { get; init; } = 1;

    public bool ShowCount => Count > 1;

    public string CountText => $"{Count} оп.";

    public byte[]? ImageData { get; init; }

    public string? Icon { get; init; }

    public string? IconColor { get; init; }
}
