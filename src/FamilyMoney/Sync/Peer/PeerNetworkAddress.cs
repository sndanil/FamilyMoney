using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace FamilyMoney.Sync.Peer;

public sealed record PeerNetworkAddress(
    string Address,
    string DisplayName,
    bool IsWireless = false)
{
    public IPAddress IpAddress => IPAddress.Parse(Address);
}

public static class PeerNetworkAddresses
{
    public static IReadOnlyList<PeerNetworkAddress> GetAvailable()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(IsUsable)
                .SelectMany(network => network.GetIPProperties().UnicastAddresses
                    .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Where(address => !IPAddress.IsLoopback(address.Address))
                    .Select(address => new PeerNetworkAddress(
                        address.Address.ToString(),
                        $"{network.Name} · {address.Address}",
                        network.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)))
                .GroupBy(address => address.Address, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderByDescending(address => address.IsWireless)
                .ThenBy(address => address.DisplayName, StringComparer.CurrentCulture)
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static bool IsUsable(NetworkInterface network) =>
        network.OperationalStatus == OperationalStatus.Up
        && network.NetworkInterfaceType is not NetworkInterfaceType.Loopback
        && network.NetworkInterfaceType is not NetworkInterfaceType.Tunnel;
}
