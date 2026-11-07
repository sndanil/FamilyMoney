using FamilyMoney.Analytics;
using System.Linq;
using Xunit;

namespace FamilyMoney.Tests;

public class NiceTicksTests
{
    [Fact]
    public void Step_UsesRoundMultipliers()
    {
        Assert.Equal(250_000m, NiceTicks.Step(1_000_000m, 4));
        Assert.Equal(200_000m, NiceTicks.Step(658_000m, 4));
        Assert.Equal(1m, NiceTicks.Step(0m, 4));
    }

    [Fact]
    public void Build_CoversRangeWithRoundValues()
    {
        var ticks = NiceTicks.Build(0m, 1_000_000m, 4);

        Assert.Equal(new decimal[] { 0m, 250_000m, 500_000m, 750_000m, 1_000_000m }, ticks);
    }

    [Fact]
    public void Build_HandlesNegativeRange()
    {
        var ticks = NiceTicks.Build(-500m, 1_500m, 4);

        Assert.Equal(new decimal[] { -500m, 0m, 500m, 1_000m, 1_500m }, ticks);
    }

    [Fact]
    public void Build_ReturnsSingleTickForFlatRange()
    {
        var ticks = NiceTicks.Build(100m, 100m);

        Assert.Single(ticks);
        Assert.Equal(100m, ticks.First());
    }

    [Fact]
    public void Rounding_GoesToStepBounds()
    {
        Assert.Equal(800_000m, NiceTicks.RoundDown(932_400m, 200_000m));
        Assert.Equal(1_800_000m, NiceTicks.RoundUp(1_695_600m, 200_000m));
        Assert.Equal(1_000_000m, NiceTicks.RoundUp(1_000_000m, 200_000m));
    }
}
