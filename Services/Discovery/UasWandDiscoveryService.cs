using Inductobot.Abstractions.Services;
using Inductobot.Models.Device;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Inductobot.Services.Discovery;

/// <summary>
/// UAS-WAND device discovery service implementation
/// </summary>
public class UasWandDiscoveryService : IUasWandDiscoveryService, IDisposable
{
    private readonly IUasWandDeviceService _deviceService;
    private readonly IUasWandSimulatorService _simulatorService;
    private readonly ILogger<UasWandDiscoveryService> _logger;
    private readonly List<UASDeviceInfo> _discoveredDevices = new();
    private CancellationTokenSource? _scanCts;
    private readonly SemaphoreSlim _devicesLock = new(1, 1);
    
    public event EventHandler<UASDeviceInfo>? DeviceDiscovered;
    public event EventHandler<UASDeviceInfo>? DeviceRemoved;
    public event EventHandler<bool>? ScanningStateChanged;
    
    public IReadOnlyList<UASDeviceInfo> DiscoveredDevices => _discoveredDevices.AsReadOnly();
    public bool IsScanning { get; private set; }
    
    // Common ports used by UAS-WAND devices
    private readonly int[] _commonPorts = { 80, 443, 8080, 8443, 5000, 5001, 7000, 7001 };
    
    public UasWandDiscoveryService(IUasWandDeviceService deviceService, IUasWandSimulatorService simulatorService, ILogger<UasWandDiscoveryService> logger)
    {
        _deviceService = deviceService;
        _simulatorService = simulatorService;
        _logger = logger;
    }
    
    public async Task StartScanAsync(CancellationToken cancellationToken = default)
    {
        if (IsScanning)
        {
            _logger.LogWarning("Scan already in progress");
            return;
        }
        
        _scanCts?.Cancel();
        _scanCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        IsScanning = true;
        ScanningStateChanged?.Invoke(this, true);
        _logger.LogInformation("Starting UAS-WAND device discovery scan");
        
        try
        {
            await ScanForDevicesAsync(_scanCts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Device scan was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during device discovery scan");
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
        if (IsScanning)
        {
            _logger.LogInformation("Stopping UAS-WAND device discovery scan");
            _scanCts?.Cancel();
        }
    }
    
    public async Task<bool> TestConnectionAsync(UASDeviceInfo device, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _deviceService.TestConnectionAsync(device.IpAddress, device.Port, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test connection to device {DeviceName} at {IpAddress}:{Port}", 
                device.Name, device.IpAddress, device.Port);
            return false;
        }
    }
    
    public async Task ForceDiscoverSimulatorAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Forcing simulator discovery check");
        await CheckForSimulatorAsync(cancellationToken);
    }

    public int GetDeviceDiscoveredSubscriberCount()
    {
        return DeviceDiscovered?.GetInvocationList()?.Length ?? 0;
    }

    public async Task RefreshDeviceAsync(UASDeviceInfo device, CancellationToken cancellationToken = default)
    {
        try
        {
            var isOnline = await TestConnectionAsync(device, cancellationToken);
            
            await _devicesLock.WaitAsync(cancellationToken);
            try
            {
                var existingDevice = _discoveredDevices.FirstOrDefault(d => 
                    d.IpAddress == device.IpAddress && d.Port == device.Port);
                
                if (existingDevice != null)
                {
                    existingDevice.IsOnline = isOnline;
                    existingDevice.LastSeen = DateTime.Now;
                    
                    if (isOnline)
                    {
                        DeviceDiscovered?.Invoke(this, existingDevice);
                    }
                }
            }
            finally
            {
                _devicesLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh device {DeviceName}", device.Name);
        }
    }
    
    public async Task<bool> AddDeviceManuallyAsync(string ipAddress, int port, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IPAddress.TryParse(ipAddress, out _))
            {
                _logger.LogWarning("Invalid IP address format: {IpAddress}", ipAddress);
                return false;
            }
            
            if (port <= 0 || port > 65535)
            {
                _logger.LogWarning("Invalid port number: {Port}", port);
                return false;
            }
            
            var device = new UASDeviceInfo
            {
                DeviceId = Guid.NewGuid().ToString(),
                IpAddress = ipAddress,
                Port = port,
                Name = $"UAS-WAND_{ipAddress}:{port}",
                IsOnline = false,
                LastSeen = DateTime.Now
            };
            
            // Test connection to verify device
            var isOnline = await TestConnectionAsync(device, cancellationToken);
            device.IsOnline = isOnline;
            
            await _devicesLock.WaitAsync(cancellationToken);
            try
            {
                // Check if device already exists
                var exists = _discoveredDevices.Any(d => 
                    d.IpAddress == device.IpAddress && d.Port == device.Port);
                
                if (!exists)
                {
                    _discoveredDevices.Add(device);
                    DeviceDiscovered?.Invoke(this, device);
                    _logger.LogInformation("Manually added UAS-WAND device: {DeviceName} ({Online})", 
                        device.Name, isOnline ? "Online" : "Offline");
                    return true;
                }
                else
                {
                    _logger.LogInformation("UAS-WAND device already exists: {IpAddress}:{Port}", ipAddress, port);
                    return false;
                }
            }
            finally
            {
                _devicesLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add device manually: {IpAddress}:{Port}", ipAddress, port);
            return false;
        }
    }
    
    public void RemoveDevice(UASDeviceInfo device)
    {
        try
        {
            _devicesLock.Wait();
            try
            {
                var toRemove = _discoveredDevices.FirstOrDefault(d => 
                    d.IpAddress == device.IpAddress && d.Port == device.Port);
                
                if (toRemove != null)
                {
                    _discoveredDevices.Remove(toRemove);
                    DeviceRemoved?.Invoke(this, toRemove);
                    _logger.LogInformation("Removed UAS-WAND device: {DeviceName}", toRemove.Name);
                }
            }
            finally
            {
                _devicesLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove device: {DeviceName}", device.Name);
        }
    }
    
    public void ClearDevices()
    {
        try
        {
            _devicesLock.Wait();
            try
            {
                var devices = _discoveredDevices.ToList();
                _discoveredDevices.Clear();
                
                foreach (var device in devices)
                {
                    DeviceRemoved?.Invoke(this, device);
                }
                
                _logger.LogInformation("Cleared all discovered UAS-WAND devices");
            }
            finally
            {
                _devicesLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear devices");
        }
    }
    
    private async Task ScanForDevicesAsync(CancellationToken cancellationToken)
    {
        // First, check for running simulator
        await CheckForSimulatorAsync(cancellationToken);
        
        if (cancellationToken.IsCancellationRequested)
            return;
        
        // Then scan the network for physical devices
        await ScanNetworkForDevicesAsync(cancellationToken);
    }
    
    private async Task CheckForSimulatorAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Checking for running simulator...");
            var status = _simulatorService.GetStatus();
            _logger.LogInformation("Simulator status - IsRunning: {IsRunning}, IP: {IP}, Port: {Port}", 
                status.IsRunning, status.IPAddress, status.Port);
            
            if (status.IsRunning)
            {
                _logger.LogInformation("Found running UAS-WAND simulator at {Address}:{Port}", status.IPAddress, status.Port);
                
                var simulatorDevice = new UASDeviceInfo
                {
                    DeviceId = "UAS-WAND-SIMULATOR",
                    IpAddress = "127.0.0.1", // Force localhost for reliable connection
                    Port = status.Port,
                    Name = status.DeviceName ?? "UAS-WAND_Simulator",
                    FirmwareVersion = status.FirmwareVersion,
                    SerialNumber = "SIM-001",
                    Type = Models.Device.DeviceType.Simulator,
                    IsOnline = true,
                    LastSeen = DateTime.Now,
                    DeviceType = "Simulator"
                };
                
                await _devicesLock.WaitAsync(cancellationToken);
                try
                {
                    // Remove any existing simulator entry
                    var existingSimulator = _discoveredDevices.FirstOrDefault(d => d.DeviceId == "UAS-WAND-SIMULATOR");
                    if (existingSimulator != null)
                    {
                        _discoveredDevices.Remove(existingSimulator);
                    }
                    
                    // Add current simulator
                    _discoveredDevices.Add(simulatorDevice);
                    _logger.LogInformation("About to invoke DeviceDiscovered event for simulator. Subscribers: {Count}", 
                        DeviceDiscovered?.GetInvocationList()?.Length ?? 0);
                    DeviceDiscovered?.Invoke(this, simulatorDevice);
                    _logger.LogInformation("DeviceDiscovered event invoked for UAS-WAND simulator");
                }
                finally
                {
                    _devicesLock.Release();
                }
            }
            else
            {
                // Remove simulator if it was previously discovered but is no longer running
                await _devicesLock.WaitAsync(cancellationToken);
                try
                {
                    var existingSimulator = _discoveredDevices.FirstOrDefault(d => d.DeviceId == "UAS-WAND-SIMULATOR");
                    if (existingSimulator != null)
                    {
                        _discoveredDevices.Remove(existingSimulator);
                        DeviceRemoved?.Invoke(this, existingSimulator);
                        _logger.LogInformation("Removed stopped UAS-WAND simulator from discovered devices");
                    }
                }
                finally
                {
                    _devicesLock.Release();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for simulator during discovery");
        }
    }
    
    private async Task ScanNetworkForDevicesAsync(CancellationToken cancellationToken)
    {
        var localIpAddresses = GetLocalIPAddresses();
        _logger.LogInformation("Found {Count} local network interfaces to scan", localIpAddresses.Count);
        
        foreach (var localIp in localIpAddresses)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            _logger.LogDebug("Scanning subnet for local IP: {LocalIp}", localIp);
            await ScanSubnetAsync(localIp, cancellationToken);
        }
    }
    
    private async Task ScanSubnetAsync(IPAddress localIp, CancellationToken cancellationToken)
    {
        try
        {
            var subnet = GetSubnetFromIP(localIp);
            _logger.LogDebug("Scanning subnet: {Subnet}", subnet);
            
            var tasks = new List<Task>();
            
            // Scan common IP range (192.168.x.1 to 192.168.x.254)
            for (int host = 1; host <= 254; host++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                
                var ipToScan = $"{subnet}.{host}";
                tasks.Add(ScanHostAsync(ipToScan, cancellationToken));
                
                // Limit concurrent scans to avoid overwhelming the network
                if (tasks.Count >= 10)
                {
                    await Task.WhenAny(tasks);
                    tasks.RemoveAll(t => t.IsCompleted);
                }
            }
            
            // Wait for remaining tasks
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning subnet for IP {LocalIp}", localIp);
        }
    }
    
    private async Task ScanHostAsync(string ipAddress, CancellationToken cancellationToken)
    {
        try
        {
            // First, ping the host to see if it's alive
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ipAddress, 1000);
            
            if (reply.Status == IPStatus.Success)
            {
                // Host is alive, try connecting to common ports
                foreach (var port in _commonPorts)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    
                    if (await TestPortAsync(ipAddress, port, cancellationToken))
                    {
                        if (await ValidateUasWandDeviceAsync(ipAddress, port, cancellationToken))
                        {
                            await AddDiscoveredDeviceAsync(ipAddress, port, cancellationToken);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error scanning host {IpAddress}", ipAddress);
        }
    }
    
    private async Task<bool> TestPortAsync(string ipAddress, int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));
            
            await client.ConnectAsync(ipAddress, port, timeoutCts.Token);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }
    
    private async Task<bool> ValidateUasWandDeviceAsync(string ipAddress, int port, CancellationToken cancellationToken)
    {
        try
        {
            // Test connection to device and attempt to get device info to validate it's a UAS-WAND device
            var connectionResult = await _deviceService.TestConnectionAsync(ipAddress, port, cancellationToken);
            if (!connectionResult)
            {
                return false;
            }
            
            // For now, if the device responds to our test connection, we'll consider it a potential UAS-WAND
            // In a real implementation, we might send a device info command to verify the device name
            // starts with "UAS-WAND"
            _logger.LogDebug("Validated potential UAS-WAND device at {IpAddress}:{Port}", ipAddress, port);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to validate UAS-WAND device at {IpAddress}:{Port}", ipAddress, port);
            return false;
        }
    }
    
    private async Task AddDiscoveredDeviceAsync(string ipAddress, int port, CancellationToken cancellationToken)
    {
        try
        {
            var device = new UASDeviceInfo
            {
                DeviceId = Guid.NewGuid().ToString(),
                IpAddress = ipAddress,
                Port = port,
                Name = $"UAS-WAND_{ipAddress}:{port}",
                IsOnline = true,
                LastSeen = DateTime.Now
            };
            
            await _devicesLock.WaitAsync(cancellationToken);
            try
            {
                // Check if device already exists
                var exists = _discoveredDevices.Any(d => 
                    d.IpAddress == device.IpAddress && d.Port == device.Port);
                
                if (!exists)
                {
                    _discoveredDevices.Add(device);
                    DeviceDiscovered?.Invoke(this, device);
                    _logger.LogInformation("Discovered UAS-WAND device: {DeviceName}", device.Name);
                }
            }
            finally
            {
                _devicesLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add discovered device: {IpAddress}:{Port}", ipAddress, port);
        }
    }
    
    private List<IPAddress> GetLocalIPAddresses()
    {
        var localIps = new List<IPAddress>();
        
        try
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            
            foreach (var ni in networkInterfaces)
            {
                if (ni.OperationalStatus == OperationalStatus.Up && 
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    var ipProperties = ni.GetIPProperties();
                    foreach (var ip in ipProperties.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            localIps.Add(ip.Address);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate network interfaces for UAS-WAND discovery");
            // No hardcoded fallback - if we can't detect network interfaces, 
            // we can't discover DHCP-assigned UAS-WAND devices
        }
        
        return localIps;
    }
    
    private static string GetSubnetFromIP(IPAddress ip)
    {
        var ipBytes = ip.GetAddressBytes();
        return $"{ipBytes[0]}.{ipBytes[1]}.{ipBytes[2]}";
    }
    
    public void Dispose()
    {
        StopScan();
        _scanCts?.Dispose();
        _devicesLock?.Dispose();
    }
}