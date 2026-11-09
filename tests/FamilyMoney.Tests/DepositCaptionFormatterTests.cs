using FamilyMoney.Configuration;
using FamilyMoney.Models;
using FamilyMoney.Sync;
using FamilyMoney.ViewModels;
using System.Globalization;
using Xunit;

namespace FamilyMoney.Tests;

public class DepositCaptionFormatterTests
{
    public DepositCaptionFormatterTests()
    {
        CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
    }

    [Fact]
    public void Empty_HasNoCaption()
    {
        var caption = DepositCaptionFormatter.Format(null, null, 7, new DateTime(2026, 7, 1));

        Assert.Null(caption.Text);
        Assert.Equal(DepositTermKind.None, caption.Kind);
    }

    [Fact]
    public void RateOnly_ShowsPercentWithoutTerm()
    {
        var caption = DepositCaptionFormatter.Format(null, 16.5m, 7, new DateTime(2026, 7, 1));

        Assert.Equal("16,5%", caption.Text);
        Assert.Equal(DepositTermKind.None, caption.Kind);
    }

    [Fact]
    public void WholeRate_DropsTrailingZeros()
    {
        var caption = DepositCaptionFormatter.Format(null, 16m, 7, new DateTime(2026, 7, 1));

        Assert.Equal("16%", caption.Text);
    }

    [Fact]
    public void ZeroRate_IsShown()
    {
        var caption = DepositCaptionFormatter.Format(null, 0m, 7, new DateTime(2026, 7, 1));

        Assert.Equal("0%", caption.Text);
    }

    [Fact]
    public void DateOnly_StaysCalmOutsideThreshold()
    {
        var caption = DepositCaptionFormatter.Format(new DateTime(2026, 7, 13), null, 7, new DateTime(2026, 7, 1));

        Assert.Equal("до 13.07.2026", caption.Text);
        Assert.Equal(DepositTermKind.Calm, caption.Kind);
    }

    [Fact]
    public void Soon_IncludesRemainingDaysAndRate()
    {
        var caption = DepositCaptionFormatter.Format(new DateTime(2026, 7, 13), 16m, 14, new DateTime(2026, 7, 1));

        Assert.Equal("до 13.07.2026 · 12 дн. · 16%", caption.Text);
        Assert.Equal(DepositTermKind.Soon, caption.Kind);
    }

    [Fact]
    public void DefaultThreshold_IsSevenDays()
    {
        var inside = DepositCaptionFormatter.Format(new DateTime(2026, 7, 8), null, 7, new DateTime(2026, 7, 1));
        var outside = DepositCaptionFormatter.Format(new DateTime(2026, 7, 9), null, 7, new DateTime(2026, 7, 1));

        Assert.Equal(DepositTermKind.Soon, inside.Kind);
        Assert.Equal("до 08.07.2026 · 7 дн.", inside.Text);
        Assert.Equal(DepositTermKind.Calm, outside.Kind);
    }

    [Fact]
    public void ZeroThreshold_WarnsOnlyOnTheEndDate()
    {
        var today = new DateTime(2026, 7, 13);
        var onEnd = DepositCaptionFormatter.Format(today, null, 0, today);
        var tomorrow = DepositCaptionFormatter.Format(today.AddDays(1), null, 0, today);

        Assert.Equal(DepositTermKind.Soon, onEnd.Kind);
        Assert.Equal("до 13.07.2026 · 0 дн.", onEnd.Text);
        Assert.Equal(DepositTermKind.Calm, tomorrow.Kind);
    }

    [Fact]
    public void Expired_UsesPrefixWithoutRemainingDays()
    {
        var caption = DepositCaptionFormatter.Format(new DateTime(2026, 7, 1), 16.5m, 7, new DateTime(2026, 7, 2));

        Assert.Equal("истёк 01.07.2026 · 16,5%", caption.Text);
        Assert.Equal(DepositTermKind.Expired, caption.Kind);
    }

    [Theory]
    [InlineData("", true, null)]
    [InlineData("   ", true, null)]
    [InlineData("16", true, "16")]
    [InlineData("16,5", true, "16.5")]
    [InlineData("16.50", true, "16.5")]
    [InlineData("16%", true, "16")]
    [InlineData("0", true, "0")]
    [InlineData("-1", false, null)]
    [InlineData("ставка", false, null)]
    public void TryParseRate_AcceptsEmptyAndNonNegative(string text, bool expected, string? rateText)
    {
        var parsed = DepositCaptionFormatter.TryParseRate(text, out var rate);

        Assert.Equal(expected, parsed);
        Assert.Equal(rateText == null ? null : decimal.Parse(rateText, CultureInfo.InvariantCulture), rate);
    }

    [Fact]
    public void MissingWarningDays_UsesSevenAndKeepsExplicitZero()
    {
        Assert.Equal(7, new RootConfiguration().GetDepositWarningDays());
        Assert.Equal(0, new RootConfiguration { DepositWarningDays = 0 }.GetDepositWarningDays());
        Assert.Equal(7, new RootConfiguration { DepositWarningDays = -3 }.GetDepositWarningDays());
        Assert.Equal(RootConfiguration.MaxDepositWarningDays, new RootConfiguration { DepositWarningDays = 99999 }.GetDepositWarningDays());
    }

    [Fact]
    public void AccountSync_RoundTripsDepositFields()
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Депозит",
            EndsOn = new DateTime(2026, 7, 13),
            AnnualRate = 16.5m,
        };

        var restored = (Account)SyncEntitySerializer.Deserialize(SyncEntitySerializer.CreateRecord(account))!;

        Assert.Equal(new DateTime(2026, 7, 13), restored.EndsOn);
        Assert.Equal(16.5m, restored.AnnualRate);
    }

    [Fact]
    public void AccountSync_LeavesMissingDepositFieldsEmpty()
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Карта",
        };

        var restored = (Account)SyncEntitySerializer.Deserialize(SyncEntitySerializer.CreateRecord(account))!;

        Assert.Null(restored.EndsOn);
        Assert.Null(restored.AnnualRate);
    }
}
