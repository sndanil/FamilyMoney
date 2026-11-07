using FamilyMoney.ViewModels;
using System;
using System.Linq;
using Xunit;

namespace FamilyMoney.Tests;

public class TimelineScopeCatalogTests
{
    [Fact]
    public void Build_IncludesTotalGroupsAndAccountsWithIcons()
    {
        var total = new AccountViewModel { Name = "Всего", IsGroup = true };
        var group = new AccountViewModel { Id = Guid.NewGuid(), Name = "Карты", IsGroup = true };
        var account = new AccountViewModel { Id = Guid.NewGuid(), Name = "Наличные", ImageData = [1, 2, 3] };

        group.Children.Add(account);
        total.Children.Add(group);

        var scopes = TimelineScopeCatalog.Build(total);

        Assert.Equal(3, scopes.Count);

        Assert.Equal("Всего", scopes[0].Name);
        Assert.Null(scopes[0].AccountId);
        Assert.False(scopes[0].IsGroup);

        Assert.Equal("Карты", scopes[1].Name);
        Assert.True(scopes[1].IsGroup);
        Assert.Equal(0, scopes[1].Level);

        Assert.Equal("Наличные", scopes[2].Name);
        Assert.Equal(1, scopes[2].Level);
        Assert.Equal(account.ImageData, scopes[2].ImageData);
        Assert.Equal(18d, scopes[2].IndentWidth);
    }

    [Fact]
    public void Build_SkipsHiddenAccounts()
    {
        var total = new AccountViewModel { Name = "Всего", IsGroup = true };
        var group = new AccountViewModel { Id = Guid.NewGuid(), Name = "Карты", IsGroup = true };
        var visible = new AccountViewModel { Id = Guid.NewGuid(), Name = "Видимый" };
        var hidden = new AccountViewModel { Id = Guid.NewGuid(), Name = "Скрытый", IsHidden = true };

        group.Children.Add(visible);
        group.Children.Add(hidden);
        total.Children.Add(group);

        var scopes = TimelineScopeCatalog.Build(total);

        Assert.Equal(["Всего", "Карты", "Видимый"], scopes.Select(s => s.Name));
    }

    [Fact]
    public void Build_HiddenGroupHidesItsSubtree()
    {
        var total = new AccountViewModel { Name = "Всего", IsGroup = true };
        var hiddenGroup = new AccountViewModel { Id = Guid.NewGuid(), Name = "Скрытая группа", IsGroup = true, IsHidden = true };
        var child = new AccountViewModel { Id = Guid.NewGuid(), Name = "Внутри" };
        var alone = new AccountViewModel { Id = Guid.NewGuid(), Name = "Отдельный" };

        hiddenGroup.Children.Add(child);
        total.Children.Add(hiddenGroup);
        total.Children.Add(alone);

        var scopes = TimelineScopeCatalog.Build(total);

        Assert.Equal(["Всего", "Отдельный"], scopes.Select(s => s.Name));
    }
}
