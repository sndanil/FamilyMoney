using CommunityToolkit.Mvvm.ComponentModel;

namespace FamilyMoney.ViewModels;

/// <summary>
/// Данные одной карточки «Топ расходов». Список карточек заменяется целиком при пересчёте,
/// отдельно наблюдаемым нужно только выделение.
/// </summary>
public partial class TopExpenseCardViewModel : ViewModelBase
{
    /// <summary>Категория позиции; null — операции без категории.</summary>
    public Guid? CategoryId { get; init; }

    /// <summary>Подкатегория позиции; null — операции без подкатегории.</summary>
    public Guid? SubCategoryId { get; init; }

    public required string CategoryName { get; init; }

    public string SubCategoryName { get; init; } = string.Empty;

    public string Comment { get; init; } = string.Empty;

    /// <summary>
    /// Комментарий одной строкой: в карточке фиксированная высота, и переводы строк
    /// из длинного комментария её ломали.
    /// </summary>
    public string CommentSingleLine => Comment
        .ReplaceLineEndings(" ")
        .Trim();

    public decimal Sum { get; init; }

    /// <summary>Доля от всех расходов за период, 0..1.</summary>
    public decimal Percent { get; init; }

    public int Count { get; init; } = 1;

    public bool ShowCount => Count > 1;

    public string CountText => $"{Count} оп.";

    public byte[]? ImageData { get; init; }

    public string? Icon { get; init; }

    public string? IconColor { get; init; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
