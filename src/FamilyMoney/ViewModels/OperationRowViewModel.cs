using System;
using System.Collections.Generic;

namespace FamilyMoney.ViewModels;

/// <summary>Одна операция в панели выбранной карточки.</summary>
public sealed class OperationRowViewModel
{
    /// <summary>
    /// Комментарий операции. Показываем, только когда карточка объединяет разные
    /// комментарии: иначе он один на всю карточку и стоит в её шапке.
    /// </summary>
    public string Comment { get; init; } = string.Empty;

    public bool ShowComment { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public bool HasTags => Tags.Count > 0;

    public decimal Sum { get; init; }

    /// <summary>Счёт операции: его имя и оформление показываем в строке.</summary>
    public string AccountName { get; init; } = string.Empty;

    public byte[]? ImageData { get; init; }

    public string? Icon { get; init; }

    public string? IconColor { get; init; }
}
