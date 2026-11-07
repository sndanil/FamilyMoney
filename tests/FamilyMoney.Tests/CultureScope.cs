using System;
using System.Globalization;

namespace FamilyMoney.Tests;

/// <summary>Временно подменяет текущую культуру — форматирование денег от неё зависит.</summary>
internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _previous;

    public CultureScope(string cultureName)
    {
        _previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _previous;
    }
}
