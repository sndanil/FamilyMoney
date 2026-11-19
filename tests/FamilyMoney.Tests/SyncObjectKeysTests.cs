using FamilyMoney.Sync;
using Xunit;

namespace FamilyMoney.Tests;

public sealed class SyncObjectKeysTests
{
    [Theory]
    [InlineData("familymoney/id/devices/a/manifest.json", "familymoney/id/devices/a/manifest.json")]
    [InlineData("/familymoney/id/devices/a/manifest.json", "familymoney/id/devices/a/manifest.json")]
    [InlineData("//familymoney/id/snapshots/base-1.json", "familymoney/id/snapshots/base-1.json")]
    public void Normalize_ReturnsBucketRelativeKey(string input, string expected)
    {
        Assert.Equal(expected, SyncObjectKeys.Normalize(input));
    }
}
