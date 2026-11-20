using FamilyMoney.Models;
using FamilyMoney.Sync;
using FamilyMoney.Utils;
using Material.Icons;
using System;
using Xunit;

namespace FamilyMoney.Tests;

public class IconHelpersTests
{
    [Theory]
    [InlineData("material:CartOutline", IconSpecKind.Material)]
    [InlineData("CartOutline", IconSpecKind.Material)]
    [InlineData("letter:пп", IconSpecKind.Letter)]
    [InlineData("pack:banks/sber", IconSpecKind.Pack)]
    [InlineData("pack:banks/sber-card", IconSpecKind.Pack)]
    [InlineData("pack:Banks/Sber", IconSpecKind.Pack)]
    public void TryParse_RecognizesSpecs(string value, IconSpecKind expected)
    {
        Assert.True(IconSpec.TryParse(value, out var spec));
        Assert.Equal(expected, spec.Kind);
    }

    [Theory]
    [InlineData("material:123")]
    [InlineData("letter:ППП")]
    [InlineData("pack:banks")]
    [InlineData("unknown:value")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_RejectsInvalidSpecs(string? value)
    {
        Assert.False(IconSpec.TryParse(value, out _));
    }

    [Theory]
    [InlineData("Дача", "Д")]
    [InlineData("Продукты питания", "ПП")]
    [InlineData("Сбербанк России", "СР")]
    [InlineData("Дача и огород", "ДО")]
    [InlineData("ДОМ.РФ", "Д")]
    [InlineData("  ", "")]
    public void LetterSuggest_UsesWordsNotMoreThanTwo(string name, string expected)
    {
        Assert.Equal(expected, IconLetter.Suggest(name));
    }

    [Theory]
    [InlineData("#3390ec", "#3390EC")]
    [InlineData("3390EC", "#3390EC")]
    public void ColorParse_Normalizes(string value, string expected)
    {
        Assert.True(IconColor.TryParse(value, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("#12345")]
    [InlineData("зелёный")]
    [InlineData("")]
    [InlineData(null)]
    public void ColorParse_RejectsInvalid(string? value)
    {
        Assert.False(IconColor.TryParse(value, out _));
    }

    [Fact]
    public void Suggester_RecognizesProductsAndBank()
    {
        Assert.Equal("material:CartOutline", IconSuggester.Suggest("Продукты"));

        Assert.True(IconSpec.TryParse(IconSuggester.Suggest("Бензин"), out var fuel));
        Assert.Equal(IconSpecKind.Material, fuel.Kind);
        Assert.Equal(MaterialIconKind.GasStation, fuel.Material);

        Assert.Equal("pack:banks/sber", IconSuggester.Suggest("Сбербанк"));
        Assert.Equal("pack:banks/sber", IconSuggester.Suggest("Сбер"));
    }

    [Fact]
    public void Catalog_Search_FindsGasStation()
    {
        var found = IconCatalog.Search("бензин");
        Assert.Contains(found, item =>
            IconSpec.TryParse(item.Spec, out var spec) && spec.Material == MaterialIconKind.GasStation);
    }

    [Fact]
    public void SyncSerializer_RoundTripsIconAndColor()
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Сбербанк",
            Icon = "pack:banks/sber",
            IconColor = "#2E7D32",
            LastChange = DateTime.UtcNow,
        };

        var record = SyncEntitySerializer.CreateRecord(account);
        var restored = Assert.IsType<Account>(SyncEntitySerializer.Deserialize(record));

        Assert.Equal(account.Icon, restored.Icon);
        Assert.Equal(account.IconColor, restored.IconColor);
    }

    [Fact]
    public void MaterialNames_AreValid()
    {
        Assert.True(Enum.IsDefined(MaterialIconKind.CartOutline));
    }
}
