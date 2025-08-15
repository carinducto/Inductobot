using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Inductobot.Services.Core;

/// <summary>
/// Service for retrieving network information
/// </summary>
public class NetworkInfoService : INetworkInfoService
{
    private readonly ILogger<NetworkInfoService> _logger;

    public NetworkInfoService(ILogger<NetworkInfoService> logger)
    {
        _logger = logger;
    }

    public NetworkInfo GetCurrentNetworkInfo()
    {
        try
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up && 
                           ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToList();

            var primaryInterface = networkInterfaces
                .FirstOrDefault(ni => ni.GetIPProperties().GatewayAddresses.Any());

            if (primaryInterface == null)
            {
                _logger.LogWarning("No active network interface with gateway found");
                return new NetworkInfo
                {
                    IsConnected = false,
                    ErrorMessage = "No active network connection found"
                };
            }

            var ipProperties = primaryInterface.GetIPProperties();
            var unicastAddress = ipProperties.UnicastAddresses
                .FirstOrDefault(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork);

            var gatewayAddress = ipProperties.GatewayAddresses
                .FirstOrDefault(gw => gw.Address.AddressFamily == AddressFamily.InterNetwork);

            if (unicastAddress == null)
            {
                _logger.LogWarning("No IPv4 address found on primary interface");
                return new NetworkInfo
                {
                    IsConnected = false,
                    ErrorMessage = "No IPv4 address found"
                };
            }

            var localIP = unicastAddress.Address;
            var subnetMask = unicastAddress.IPv4Mask;
            var gateway = gatewayAddress?.Address;

            // Calculate network range
            var (networkAddress, broadcastAddress) = CalculateNetworkRange(localIP, subnetMask);

            return new NetworkInfo
            {
                IsConnected = true,
                InterfaceName = primaryInterface.Name,
                InterfaceDescription = primaryInterface.Description,
                LocalIPAddress = localIP.ToString(),
                SubnetMask = subnetMask?.ToString() ?? "Unknown",
                GatewayAddress = gateway?.ToString() ?? "Unknown",
                NetworkAddress = networkAddress,
                BroadcastAddress = broadcastAddress,
                IPRange = $"{networkAddress} - {broadcastAddress}",
                CIDR = CalculateCIDR(subnetMask),
                InterfaceType = primaryInterface.NetworkInterfaceType.ToString(),
                Speed = primaryInterface.Speed > 0 ? $"{primaryInterface.Speed / 1_000_000} Mbps" : "Unknown"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting network information");
            return new NetworkInfo
            {
                IsConnected = false,
                ErrorMessage = $"Error: {ex.Message}"
            };
        }
    }

    public List<NetworkInfo> GetAllNetworkInterfaces()
    {
        var networkInfos = new List<NetworkInfo>();

        try
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up && 
                           ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var ni in networkInterfaces)
            {
                var ipProperties = ni.GetIPProperties();
                var unicastAddress = ipProperties.UnicastAddresses
                    .FirstOrDefault(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork);

                if (unicastAddress != null)
                {
                    var localIP = unicastAddress.Address;
                    var subnetMask = unicastAddress.IPv4Mask;
                    var gateway = ipProperties.GatewayAddresses
                        .FirstOrDefault(gw => gw.Address.AddressFamily == AddressFamily.InterNetwork)?.Address;

                    var (networkAddress, broadcastAddress) = CalculateNetworkRange(localIP, subnetMask);

                    networkInfos.Add(new NetworkInfo
                    {
                        IsConnected = true,
                        InterfaceName = ni.Name,
                        InterfaceDescription = ni.Description,
                        LocalIPAddress = localIP.ToString(),
                        SubnetMask = subnetMask?.ToString() ?? "Unknown",
                        GatewayAddress = gateway?.ToString() ?? "Unknown",
                        NetworkAddress = networkAddress,
                        BroadcastAddress = broadcastAddress,
                        IPRange = $"{networkAddress} - {broadcastAddress}",
                        CIDR = CalculateCIDR(subnetMask),
                        InterfaceType = ni.NetworkInterfaceType.ToString(),
                        Speed = ni.Speed > 0 ? $"{ni.Speed / 1_000_000} Mbps" : "Unknown"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all network interfaces");
        }

        return networkInfos;
    }

    private static (string networkAddress, string broadcastAddress) CalculateNetworkRange(IPAddress ip, IPAddress? subnetMask)
    {
        if (subnetMask == null)
        {
            return ("Unknown", "Unknown");
        }

        try
        {
            var ipBytes = ip.GetAddressBytes();
            var maskBytes = subnetMask.GetAddressBytes();

            // Calculate network address
            var networkBytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                networkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
            }

            // Calculate broadcast address
            var broadcastBytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                broadcastBytes[i] = (byte)(networkBytes[i] | (~maskBytes[i]));
            }

            var networkAddress = new IPAddress(networkBytes).ToString();
            var broadcastAddress = new IPAddress(broadcastBytes).ToString();

            return (networkAddress, broadcastAddress);
        }
        catch
        {
            return ("Unknown", "Unknown");
        }
    }

    private static string CalculateCIDR(IPAddress? subnetMask)
    {
        if (subnetMask == null) return "Unknown";

        try
        {
            var maskBytes = subnetMask.GetAddressBytes();
            int cidr = 0;

            foreach (var b in maskBytes)
            {
                for (int i = 7; i >= 0; i--)
                {
                    if ((b & (1 << i)) != 0)
                        cidr++;
                    else
                        return $"/{cidr}";
                }
            }

            return $"/{cidr}";
        }
        catch
        {
            return "Unknown";
        }
    }
}

/// <summary>
/// Interface for network information service
/// </summary>
public interface INetworkInfoService
{
    /// <summary>
    /// Get information about the current primary network interface
    /// </summary>
    NetworkInfo GetCurrentNetworkInfo();

    /// <summary>
    /// Get information about all active network interfaces
    /// </summary>
    List<NetworkInfo> GetAllNetworkInterfaces();
}

/// <summary>
/// Network information data model
/// </summary>
public class NetworkInfo
{
    public bool IsConnected { get; set; }
    public string? InterfaceName { get; set; }
    public string? InterfaceDescription { get; set; }
    public string? LocalIPAddress { get; set; }
    public string? SubnetMask { get; set; }
    public string? GatewayAddress { get; set; }
    public string? NetworkAddress { get; set; }
    public string? BroadcastAddress { get; set; }
    public string? IPRange { get; set; }
    public string? CIDR { get; set; }
    public string? InterfaceType { get; set; }
    public string? Speed { get; set; }
    public string? ErrorMessage { get; set; }
}