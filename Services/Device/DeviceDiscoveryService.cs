using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Inductobot.Models.Device;
using Inductobot.Services.Communication;
using Microsoft.Extensions.Logging;

namespace Inductobot.Services.Device;

public class DeviceDiscoveryService : IDeviceDiscoveryService
{
    private readonly ILogger<DeviceDiscoveryService> _logger;
    private readonly ByteSnapTcpClient _tcpClient;
    private readonly List<UASDeviceInfo> _discoveredDevices = new();
    private CancellationTokenSource? _scanCts;
    
    public event EventHandler<UASDeviceInfo>? DeviceDiscovered;
    public event EventHandler<UASDeviceInfo>? DeviceRemoved;
    public event EventHandler<bool>? ScanningStateChanged;
    
    public IReadOnlyList<UASDeviceInfo> DiscoveredDevices => _discoveredDevices.AsReadOnly();
    public bool IsScanning { get; private set; }
    
    // Common ports used by UAS-WAND devices
    private readonly int[] _commonPorts = { 80, 443, 8080, 8443, 5000, 5001, 7000, 7001 };
    
    public DeviceDiscoveryService(ILogger<DeviceDiscoveryService> logger, ByteSnapTcpClient tcpClient)
    {
        _logger = logger;
        _tcpClient = tcpClient;
    }
    
    public async Task StartScanAsync(CancellationToken cancellationToken = default)
    {
        if (IsScanning)
        {
            _logger.LogWarning("Device scan already in progress");
            return;
        }
        
        try
        {
            IsScanning = true;
            ScanningStateChanged?.Invoke(this, true);
            _scanCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            _logger.LogInformation("Starting device discovery scan");
            
            // Clear previous results
            _discoveredDevices.Clear();
            
            // Get local network information
            var localIPs = await GetLocalNetworkAddressesAsync();
            
            // Scan each subnet
            var scanTasks = new List<Task>();
            foreach (var localIP in localIPs)
            {
                var subnet = GetSubnet(localIP);
                if (subnet != null)
                {
                    scanTasks.Add(ScanSubnetAsync(subnet, _scanCts.Token));
                }
            }
            
            // Also scan for known device IPs if configured
            scanTasks.Add(ScanKnownDevicesAsync(_scanCts.Token));
            
            await Task.WhenAll(scanTasks);
            
            _logger.LogInformation($"Device discovery completed. Found {_discoveredDevices.Count} devices");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during device discovery");
        }
        finally
        {
            IsScanning = false;
            ScanningStateChanged?.Invoke(this, false);
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }
    
    public void StopScan()
    {
        _scanCts?.Cancel();
    }
    
    public async Task<bool> TestConnectionAsync(UASDeviceInfo device, CancellationToken cancellationToken = default)
    {
        try
        {
            using var testClient = new TcpClient();
            testClient.ReceiveTimeout = 2000;
            testClient.SendTimeout = 2000;
            
            var connectTask = testClient.ConnectAsync(device.IpAddress, device.Port);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            
            await connectTask.WaitAsync(cts.Token);
            
            if (testClient.Connected)
            {
                testClient.Close();
                return true;
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task RefreshDeviceAsync(UASDeviceInfo device, CancellationToken cancellationToken = default)
    {
        try
        {
            var isReachable = await TestConnectionAsync(device, cancellationToken);
            
            if (isReachable)
            {
                // Try to get updated device info
                var connected = await _tcpClient.ConnectAsync(device.IpAddress, device.Port, cancellationToken);
                if (connected)
                {
                    var infoResponse = await _tcpClient.GetDeviceInfoAsync(cancellationToken);
                    if (infoResponse.IsSuccess && infoResponse.Data != null)
                    {
                        // Update device info
                        device.Name = infoResponse.Data.Name ?? device.Name;
                        device.FirmwareVersion = infoResponse.Data.FirmwareVersion;
                        device.SerialNumber = infoResponse.Data.SerialNumber;
                        device.LastSeen = DateTime.Now;
                        device.IsOnline = true;
                    }
                    await _tcpClient.DisconnectAsync();
                }
            }
            else
            {
                device.IsOnline = false;
                DeviceRemoved?.Invoke(this, device);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error refreshing device {device.Name}");
            device.IsOnline = false;
        }
    }
    
    public void AddManualDevice(string ipAddress, int port)
    {
        var device = new UASDeviceInfo
        {
            DeviceId = Guid.NewGuid().ToString(),
            IpAddress = ipAddress,
            Port = port,
            Name = $"Manual_{ipAddress}:{port}",
            IsOnline = false,
            LastSeen = DateTime.Now
        };
        
        if (!_discoveredDevices.Any(d => d.IpAddress == ipAddress && d.Port == port))
        {
            _discoveredDevices.Add(device);
            DeviceDiscovered?.Invoke(this, device);
            
            // Test connection in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await RefreshDeviceAsync(device);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error refreshing device {DeviceId} in background", device.DeviceId);
                }
            });
        }
    }
    
    public void RemoveDevice(UASDeviceInfo device)
    {
        if (_discoveredDevices.Remove(device))
        {
            DeviceRemoved?.Invoke(this, device);
        }
    }
    
    private Task<List<IPAddress>> GetLocalNetworkAddressesAsync()
    {
        var addresses = new List<IPAddress>();
        
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);
            
            foreach (var ni in interfaces)
            {
                var props = ni.GetIPProperties();
                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        addresses.Add(addr.Address);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting local network addresses");
        }
        
        return Task.FromResult(addresses);
    }
    
    private string? GetSubnet(IPAddress address)
    {
        try
        {
            var bytes = address.GetAddressBytes();
            if (bytes.Length == 4)
            {
                // Assume /24 subnet
                return $"{bytes[0]}.{bytes[1]}.{bytes[2]}";
            }
        }
        catch
        {
            // Ignore
        }
        
        return null;
    }
    
    private async Task ScanSubnetAsync(string subnet, CancellationToken cancellationToken)
    {
        var scanTasks = new List<Task>();
        
        // Scan common device IPs in subnet
        for (int i = 1; i <= 254; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            var ip = $"{subnet}.{i}";
            
            foreach (var port in _commonPorts)
            {
                scanTasks.Add(ScanDeviceAsync(ip, port, cancellationToken));
                
                // Limit concurrent scans
                if (scanTasks.Count >= 20)
                {
                    await Task.WhenAny(scanTasks);
                    scanTasks.RemoveAll(t => t.IsCompleted);
                }
            }
        }
        
        await Task.WhenAll(scanTasks);
    }
    
    private async Task ScanKnownDevicesAsync(CancellationToken cancellationToken)
    {
        // No hardcoded IPs - UAS-WAND devices get DHCP addresses from gateway
        // This method is kept for interface compatibility but performs no scanning
        // All discovery is done through subnet scanning based on PC's network interfaces
        _logger.LogDebug("Skipping known device scan - using dynamic subnet discovery only");
        await Task.CompletedTask;
    }
    
    private async Task ScanDeviceAsync(string ipAddress, int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            client.ReceiveTimeout = 1000;
            client.SendTimeout = 1000;
            
            var connectTask = client.ConnectAsync(ipAddress, port);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(500));
            
            await connectTask.WaitAsync(cts.Token);
            
            if (client.Connected)
            {
                _logger.LogInformation($"Found device at {ipAddress}:{port}");
                
                var device = new UASDeviceInfo
                {
                    DeviceId = Guid.NewGuid().ToString(),
                    IpAddress = ipAddress,
                    Port = port,
                    Name = $"Device_{ipAddress}:{port}",
                    IsOnline = true,
                    LastSeen = DateTime.Now
                };
                
                // Try to get more info about the device
                client.Close();
                
                if (await _tcpClient.ConnectAsync(ipAddress, port, cancellationToken))
                {
                    var infoResponse = await _tcpClient.GetDeviceInfoAsync(cancellationToken);
                    if (infoResponse.IsSuccess && infoResponse.Data != null)
                    {
                        device.Name = infoResponse.Data.Name ?? device.Name;
                        device.FirmwareVersion = infoResponse.Data.FirmwareVersion;
                        device.SerialNumber = infoResponse.Data.SerialNumber;
                    }
                    await _tcpClient.DisconnectAsync();
                }
                
                if (!_discoveredDevices.Any(d => d.IpAddress == ipAddress && d.Port == port))
                {
                    _discoveredDevices.Add(device);
                    DeviceDiscovered?.Invoke(this, device);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout or cancellation - expected for most IPs
        }
        catch (Exception ex)
        {
            // Connection failed - expected for most IPs
            _logger.LogTrace($"Failed to connect to {ipAddress}:{port}: {ex.Message}");
        }
    }
}