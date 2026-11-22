using FamilyMoney.Utils;
using Xunit;

namespace FamilyMoney.Tests;

public class MoneyFormatTests
{
    [Fact]
    public void Compact_UsesMillionsAndThousands()
    {
        using var culture = new CultureScope("ru-RU");

        Assert.Equal("1,64 млн", MoneyFormat.Compact(1_643_000m));
        Assert.Equal("918 тыс", MoneyFormat.Compact(918_000m));
        Assert.Equal("12,5 тыс", MoneyFormat.Compact(12_500m));
        Assert.Equal("120", MoneyFormat.Compact(120m));
        Assert.Equal("-1,2 тыс", MoneyFormat.Compact(-1_200m));
        Assert.Equal("0", MoneyFormat.Compact(0m));
    }

    [Fact]
    public void Full_KeepsTwoDecimals()
    {
        using var culture = new CultureScope("ru-RU");

        var text = MoneyFormat.Full(1_643_000.5m);

        Assert.Contains("1", text);
        Assert.Contains("643", text);
        Assert.EndsWith(",50", text);
    }

    [Fact]
    public void CompactAxis_AddsDecimalsWhenRangeIsNarrow()
    {
        using var culture = new CultureScope("ru-RU");

        Assert.Equal("1,105 млн", MoneyFormat.CompactAxis(1_105_000m, 5_000m));
        Assert.Equal("1,110 млн", MoneyFormat.CompactAxis(1_110_000m, 5_000m));
        Assert.Equal("-1,105 млн", MoneyFormat.CompactAxis(-1_105_000m, 5_000m));
        Assert.Equal("500 тыс", MoneyFormat.CompactAxis(500_000m, 100_000m));
        Assert.Equal("12,5", MoneyFormat.CompactAxis(12.5m, 0.5m));
        Assert.Equal("0", MoneyFormat.CompactAxis(0m, 5_000m));
    }

    [Fact]
    public void Signed_AddsPlusOnlyForPositive()
    {
        using var culture = new CultureScope("ru-RU");

        Assert.StartsWith("+", MoneyFormat.Signed(100m));
        Assert.StartsWith("-", MoneyFormat.Signed(-100m));
        Assert.StartsWith("0", MoneyFormat.Signed(0m));
    }
}
