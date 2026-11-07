using System;

namespace FamilyMoney.ViewModels;

/// <summary>
/// Счёт в выпадающем списке раздела «Отчёты». Своя запись вместо <see cref="AccountViewModel"/>,
/// чтобы список оставался плоским и не зависел от выделения на главном экране.
/// </summary>
public sealed record TimelineScope(Guid? AccountId, string Name, int Level, bool IsGroup, byte[]? ImageData)
{
    /// <summary>Отступ для вложенных счетов (вместо конвертера).</summary>
    public double IndentWidth => Level * 18d;
}
