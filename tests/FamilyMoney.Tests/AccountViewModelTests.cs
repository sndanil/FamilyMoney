using FamilyMoney.ViewModels;
using Xunit;

namespace FamilyMoney.Tests;

public class AccountViewModelTests
{
    [Fact]
    public void Level_DoesNotCountServiceRoot()
    {
        var total = new AccountViewModel { IsGroup = true };
        var group = new AccountViewModel { Parent = total, IsGroup = true };
        var child = new AccountViewModel { Parent = group };

        Assert.Equal(0, group.Level);
        Assert.Equal(1, child.Level);
    }
}
