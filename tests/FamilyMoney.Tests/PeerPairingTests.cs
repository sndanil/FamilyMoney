using FamilyMoney.Sync.Peer;
using Xunit;

namespace FamilyMoney.Tests;

public sealed class PeerPairingTests
{
    [Fact]
    public void PairingPayload_RoundTrips()
    {
        var payload = new PeerPairingPayload
        {
            SyncId = Guid.NewGuid(),
            Host = "192.168.1.42",
            Port = 51234,
            Fingerprint = "abcDEF_123",
            Token = PeerPairingPayload.CreateToken(),
            DeviceId = "desktop",
            DeviceName = "Ноутбук",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
        };

        var json = payload.ToJson();
        var parsed = PeerPairingPayload.TryParse(json);

        Assert.NotNull(parsed);
        Assert.Equal(payload.SyncId, parsed!.SyncId);
        Assert.Equal(payload.Host, parsed.Host);
        Assert.Equal(payload.Port, parsed.Port);
        Assert.Equal(payload.Fingerprint, parsed.Fingerprint);
        Assert.Equal(payload.Token, parsed.Token);
        Assert.Equal(payload.DeviceId, parsed.DeviceId);
    }

    [Fact]
    public void ExpiredPairingPayload_IsRejected()
    {
        var payload = new PeerPairingPayload
        {
            SyncId = Guid.NewGuid(),
            Host = "192.168.1.42",
            Port = 51234,
            Fingerprint = "abc",
            Token = "token",
            DeviceId = "desktop",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        };

        Assert.Null(PeerPairingPayload.TryParse(payload.ToJson()));
    }

    [Fact]
    public void Token_IsUrlSafeAndNonEmpty()
    {
        var token = PeerPairingPayload.CreateToken();

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
    }
}
