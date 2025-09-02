using Inductobot.Abstractions.Services;
using Inductobot.Models.Device;
using Inductobot.Models.Discovery;
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
    private readonly IConfigurationService _config;
    private readonly ILogger<UasWandDiscoveryService> _logger;
    private readonly List<UASDeviceInfo> _discoveredDevices = new();
    private CancellationTokenSource? _scanCts;
    private readonly SemaphoreSlim _devicesLock = new(1, 1);
    
    public event EventHandler<UASDeviceInfo>? DeviceDiscovered;
    public event EventHandler<UASDeviceInfo>? DeviceRemoved;
    public event EventHandler<bool>? ScanningStateChanged;
    public event EventHandler<ScanProgress>? ScanProgressChanged;
    
    public IReadOnlyList<UASDeviceInfo> DiscoveredDevices => _discoveredDevices.AsReadOnly();
    public bool IsScanning { get; private set; }
    
    // Common ports used by UAS-WAND devices (from configuration)
    private int[] CommonPorts => _config.DefaultPorts;
    
    public UasWandDiscoveryService(IUasWandDeviceService deviceService, IUasWandSimulatorService simulatorService, IConfigurationService config, ILogger<UasWandDiscoveryService> logger)
    {
        _deviceService = deviceService;
        _simulatorService = simulatorService;
        _config = config;
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
                Name = $"Manual-UAS_{ipAddress}:{port}",
                IsOnline = false,
                LastSeen = DateTime.Now,
                FirmwareVersion = "Unknown", // Will be populated if connection succeeds
                SerialNumber = "Manual"
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
        var networkInterfaces = GetLocalNetworkInterfaces();
        _logger.LogInformation("🔍 Starting UAS device discovery scan across {Count} network interfaces", networkInterfaces.Count);
        
        // Check if no network interfaces found
        if (networkInterfaces.Count == 0)
        {
            ReportProgress(new ScanProgress
            {
                CurrentStep = "No network interfaces found",
                PercentComplete = 100,
                TotalSubnets = 0,
                SubnetsScanned = 0,
                UasDevicesFound = 0,
                DebugInfo = "No active network interfaces detected. Check that your PC is connected to a network and that UAS devices are on the same network."
            });
            _logger.LogWarning("⚠️ No network interfaces found for UAS device discovery. Ensure PC is connected to network.");
            return;
        }
        
        // Emit initial progress
        var totalSubnets = networkInterfaces.Count;
        ReportProgress(new ScanProgress
        {
            CurrentStep = "Starting network scan...",
            PercentComplete = 0,
            TotalSubnets = totalSubnets,
            SubnetsScanned = 0,
            UasDevicesFound = 0,
            DebugInfo = $"Found {totalSubnets} network interface(s) to scan"
        });
        
        var devicesFound = 0;
        var totalHostsScanned = 0;
        var subnetsCompleted = 0;
        
        foreach (var (localIp, subnet, subnetMask) in networkInterfaces)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            
            var cidrNotation = GetCidrNotation(subnetMask);
            _logger.LogInformation("🌐 Scanning subnet {Subnet}/{Cidr} for network devices (local IP: {LocalIp})", 
                subnet, cidrNotation, localIp);
            
            // Report subnet start
            ReportProgress(new ScanProgress
            {
                CurrentStep = $"Scanning subnet {subnet}/{cidrNotation}",
                PercentComplete = (subnetsCompleted * 100) / totalSubnets,
                TotalSubnets = totalSubnets,
                SubnetsScanned = subnetsCompleted,
                CurrentSubnet = $"{subnet}/{cidrNotation}",
                UasDevicesFound = devicesFound,
                DebugInfo = $"Interface {localIp} → Subnet {subnet}/{cidrNotation}"
            });
                
            var (hostsScanned, foundDevices) = await ScanSubnetForUasDevicesAsync(subnet, subnetMask, cancellationToken, subnetsCompleted, totalSubnets);
            totalHostsScanned += hostsScanned;
            devicesFound += foundDevices;
            subnetsCompleted++;
        }
        
        // Report completion
        ReportProgress(new ScanProgress
        {
            CurrentStep = "Scan completed",
            PercentComplete = 100,
            TotalSubnets = totalSubnets,
            SubnetsScanned = subnetsCompleted,
            UasDevicesFound = devicesFound,
            DebugInfo = $"Scanned {totalHostsScanned} hosts across {subnetsCompleted} subnet(s)"
        });
        
        _logger.LogInformation("🎯 Device discovery completed - Found {DeviceCount} devices after scanning {TotalHosts} hosts across all subnets", 
            devicesFound, totalHostsScanned);
    }
    
    private async Task<(int hostsScanned, int devices)> ScanSubnetForUasDevicesAsync(IPAddress subnet, IPAddress subnetMask, CancellationToken cancellationToken, int currentSubnetIndex = 0, int totalSubnets = 1)
    {
        var hostsScanned = 0;
        var devicesFound = 0;
        
        try
        {
            var cidrNotation = GetCidrNotation(subnetMask);
            _logger.LogDebug("🔍 Scanning subnet {Subnet}/{Cidr} for UAS devices", subnet, cidrNotation);
            
            var hostAddresses = GetHostAddressesInSubnet(subnet, subnetMask).ToList();
            var totalHosts = hostAddresses.Count;
            _logger.LogDebug("📋 Generated {Count} host addresses to scan", totalHosts);
            
            // Report initial subnet progress
            ReportProgress(new ScanProgress
            {
                CurrentStep = $"Scanning {totalHosts} hosts in subnet {subnet}/{cidrNotation}",
                PercentComplete = (currentSubnetIndex * 100) / totalSubnets,
                TotalSubnets = totalSubnets,
                SubnetsScanned = currentSubnetIndex,
                CurrentSubnet = $"{subnet}/{cidrNotation}",
                TotalHosts = totalHosts,
                HostsScanned = 0,
                UasDevicesFound = devicesFound,
                DebugInfo = $"Preparing to scan {totalHosts} host addresses"
            });
            
            var tasks = new List<Task<bool>>();
            const int maxConcurrent = 15;
            var completedHosts = 0;
            
            // Scan all host addresses in the subnet
            foreach (var hostIp in hostAddresses)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                
                tasks.Add(ScanHostForUasDevicesAsync(hostIp.ToString(), cancellationToken));
                hostsScanned++;
                
                // Limit concurrent scans to avoid overwhelming the network
                if (tasks.Count >= maxConcurrent)
                {
                    var completed = await Task.WhenAny(tasks);
                    if (await completed)
                    {
                        devicesFound++;
                    }
                    tasks.RemoveAll(t => t.IsCompleted);
                    completedHosts = hostsScanned - tasks.Count;
                    
                    // Report progress periodically
                    if (completedHosts % 20 == 0 || completedHosts == totalHosts)
                    {
                        var subnetProgress = ((currentSubnetIndex * 100) + ((completedHosts * 100) / totalHosts)) / totalSubnets;
                        ReportProgress(new ScanProgress
                        {
                            CurrentStep = $"Scanning subnet {subnet}/{cidrNotation} ({completedHosts}/{totalHosts} hosts)",
                            PercentComplete = subnetProgress,
                            TotalSubnets = totalSubnets,
                            SubnetsScanned = currentSubnetIndex,
                            CurrentSubnet = $"{subnet}/{cidrNotation}",
                            TotalHosts = totalHosts,
                            HostsScanned = completedHosts,
                            UasDevicesFound = devicesFound,
                            DebugInfo = $"Scanned {completedHosts} of {totalHosts} hosts, found {devicesFound} devices"
                        });
                    }
                }
            }
            
            // Wait for remaining tasks and count results
            var remainingResults = await Task.WhenAll(tasks);
            devicesFound += remainingResults.Count(result => result);
            
            // Final subnet progress
            var finalSubnetProgress = ((currentSubnetIndex + 1) * 100) / totalSubnets;
            ReportProgress(new ScanProgress
            {
                CurrentStep = $"Completed subnet {subnet}/{cidrNotation}",
                PercentComplete = finalSubnetProgress,
                TotalSubnets = totalSubnets,
                SubnetsScanned = currentSubnetIndex + 1,
                CurrentSubnet = $"{subnet}/{cidrNotation}",
                TotalHosts = totalHosts,
                HostsScanned = hostsScanned,
                UasDevicesFound = devicesFound,
                DebugInfo = $"Found {devicesFound} devices from {hostsScanned} hosts in this subnet"
            });
            
            _logger.LogInformation("📊 Subnet {Subnet}/{Cidr} scan complete: Found {DeviceCount} devices from {HostsScanned} hosts", 
                subnet, cidrNotation, devicesFound, hostsScanned);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning subnet {Subnet}/{Cidr} for UAS devices", subnet, GetCidrNotation(subnetMask));
        }
        
        return (hostsScanned, devicesFound);
    }
    
    private async Task<bool> ScanHostForUasDevicesAsync(string ipAddress, CancellationToken cancellationToken)
    {
        try
        {
            // First, ping the host to see if it's alive (quick timeout for faster scanning)
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ipAddress, 800); // Reduced timeout for faster scanning
            
            if (reply.Status == IPStatus.Success)
            {
                _logger.LogTrace("📡 Host {IpAddress} responded to ping, testing UAS device ports", ipAddress);
                
                // Host is alive, try connecting to common UAS device ports
                foreach (var port in CommonPorts)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    
                    if (await TestPortAsync(ipAddress, port, cancellationToken))
                    {
                        _logger.LogTrace("🔌 Port {Port} open on {IpAddress}, attempting device discovery", port, ipAddress);
                        
                        // Try UAS validation on port 443 (common UAS port)
                        if (port == 443)
                        {
                            var uasDeviceInfo = await ValidateAndGetUasDeviceInfoAsync(ipAddress, port, cancellationToken);
                            if (uasDeviceInfo != null)
                            {
                                await AddDiscoveredDeviceAsync(uasDeviceInfo, cancellationToken);
                                return true; // Found a verified UAS device
                            }
                            
                            // Port 443 but failed UAS validation - treat as generic device
                            var genericDevice = await CreateGenericDeviceInfoAsync(ipAddress, port);
                            if (genericDevice != null)
                            {
                                await AddDiscoveredDeviceAsync(genericDevice, cancellationToken);
                                return true; // Found a generic device on port 443
                            }
                        }
                        else
                        {
                            // Non-443 ports are treated as generic devices only
                            var genericDevice = await CreateGenericDeviceInfoAsync(ipAddress, port);
                            if (genericDevice != null)
                            {
                                await AddDiscoveredDeviceAsync(genericDevice, cancellationToken);
                                return true; // Found a generic device
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error scanning host {IpAddress} for UAS devices", ipAddress);
        }
        
        return false;
    }
    
    private async Task<UASDeviceInfo?> CreateGenericDeviceInfoAsync(string ipAddress, int port)
    {
        try
        {
            _logger.LogDebug("🔍 Creating device entry for {IpAddress}:{Port}", ipAddress, port);
            
            // Try to determine device name via DNS lookup
            string deviceName;
            bool isUasDevice = false;
            
            try
            {
                var hostEntry = await System.Net.Dns.GetHostEntryAsync(ipAddress);
                deviceName = hostEntry.HostName ?? $"Device_{ipAddress}";
                
                // Check if this is a UAS device based on hostname pattern
                if (deviceName.ToLower().Contains("uas.lan") || deviceName.ToLower().Contains("uas"))
                {
                    isUasDevice = true;
                    _logger.LogDebug("📡 Hostname '{DeviceName}' indicates UAS device", deviceName);
                }
            }
            catch
            {
                deviceName = $"Device_{ipAddress}";
            }
            
            // Do NOT assume port 443 means UAS device - only hostname/name matching
            var deviceType = isUasDevice ? "UAS Device" : "Generic Device";
            var enumType = isUasDevice ? Models.Device.DeviceType.WandV3 : Models.Device.DeviceType.Generic;
            
            _logger.LogInformation("✅ Discovered device: '{DeviceName}' at {IpAddress}:{Port} ({DeviceType})", 
                deviceName, ipAddress, port, deviceType);
            
            var discoveredDevice = new UASDeviceInfo
            {
                DeviceId = isUasDevice ? $"UAS_{ipAddress}_{port}" : $"GENERIC_{ipAddress}_{port}",
                IpAddress = ipAddress,
                Port = port,
                Name = deviceName,
                FirmwareVersion = isUasDevice ? "Unknown" : "N/A",
                SerialNumber = isUasDevice ? "Unknown" : "N/A",
                Type = enumType,
                IsOnline = true,
                LastSeen = DateTime.Now,
                ConnectionState = Models.Device.ConnectionState.Disconnected,
                DeviceType = deviceType
            };
            
            return discoveredDevice;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "❌ Failed to create device entry for {IpAddress}:{Port}", ipAddress, port);
            return null;
        }
    }
    
    private async Task<bool> TestPortAsync(string ipAddress, int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(1500)); // Faster timeout for discovery scanning
            
            await client.ConnectAsync(ipAddress, port, timeoutCts.Token);
            return client.Connected;
        }
        catch (OperationCanceledException)
        {
            // Timeout or cancellation - not an error for discovery
            return false;
        }
        catch (SocketException)
        {
            // Connection refused or network unreachable - expected for most IPs
            return false;
        }
        catch
        {
            // Other exceptions - treat as port not available
            return false;
        }
    }
    
    private async Task<UASDeviceInfo?> ValidateAndGetUasDeviceInfoAsync(string ipAddress, int port, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("🔍 Validating device at {IpAddress}:{Port} for UAS device characteristics", ipAddress, port);
            
            // First test basic connectivity
            var connectionResult = await _deviceService.TestConnectionAsync(ipAddress, port, cancellationToken);
            if (!connectionResult)
            {
                _logger.LogDebug("❌ Basic connectivity test failed for {IpAddress}:{Port}", ipAddress, port);
                return null;
            }
            
            // Now attempt to get device information to verify it's a UAS device
            try
            {
                // Temporarily connect to the device to get its info
                var wasConnected = _deviceService.IsConnected;
                var previousDevice = _deviceService.CurrentDevice;
                
                // Connect and get device info
                var connectResult = await _deviceService.ConnectToDeviceAsync(ipAddress, port, cancellationToken);
                if (connectResult && _deviceService.CurrentDevice != null)
                {
                    var deviceInfo = _deviceService.CurrentDevice;
                    var deviceName = deviceInfo.Name?.ToLower() ?? "";
                    
                    _logger.LogDebug("📡 Retrieved device info - Name: '{DeviceName}', IP: {IP}, Port: {Port}", 
                        deviceInfo.Name, deviceInfo.IpAddress, deviceInfo.Port);
                    
                    // Check if the device name contains "uas" (case insensitive)
                    var isUasDevice = deviceName.Contains("uas");
                    
                    // Create a copy of the device info for discovery - include ALL devices now
                    _logger.LogInformation("✅ Discovered device: '{DeviceName}' at {IpAddress}:{Port} {DeviceType}", 
                        deviceInfo.Name, ipAddress, port, isUasDevice ? "(UAS Device)" : "(Generic Device)");
                    
                    var discoveredDevice = new UASDeviceInfo
                    {
                        DeviceId = deviceInfo.DeviceId ?? Guid.NewGuid().ToString(),
                        IpAddress = deviceInfo.IpAddress,
                        Port = deviceInfo.Port,
                        Name = deviceInfo.Name,
                        FirmwareVersion = deviceInfo.FirmwareVersion,
                        SerialNumber = deviceInfo.SerialNumber,
                        Type = deviceInfo.Type,
                        IsOnline = true,
                        LastSeen = DateTime.Now,
                        ConnectionState = Models.Device.ConnectionState.Disconnected,
                        // Add a property to distinguish UAS vs generic devices if needed
                        DeviceType = isUasDevice ? "UAS Device" : "Generic Device"
                    };
                    
                    // Disconnect from the validation device if we weren't originally connected
                    if (!wasConnected)
                    {
                        await _deviceService.DisconnectAsync();
                        _logger.LogDebug("🔌 Disconnected from validation device {IpAddress}:{Port}", ipAddress, port);
                    }
                    else if (previousDevice != null && (previousDevice.IpAddress != ipAddress || previousDevice.Port != port))
                    {
                        // Restore previous connection if it was different
                        await _deviceService.ConnectToDeviceAsync(previousDevice.IpAddress, previousDevice.Port, cancellationToken);
                        _logger.LogDebug("🔄 Restored previous connection to {PrevIP}:{PrevPort}", 
                            previousDevice.IpAddress, previousDevice.Port);
                    }
                    
                    return discoveredDevice;
                }
                else
                {
                    _logger.LogDebug("❌ Could not retrieve device info from {IpAddress}:{Port} - connection or info request failed", 
                        ipAddress, port);
                    return null;
                }
            }
            catch (Exception deviceInfoEx)
            {
                _logger.LogDebug(deviceInfoEx, "❌ Exception while retrieving device info from {IpAddress}:{Port}", ipAddress, port);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "❌ Failed to validate UAS device at {IpAddress}:{Port}", ipAddress, port);
            return null;
        }
    }
    
    private async Task AddDiscoveredDeviceAsync(UASDeviceInfo device, CancellationToken cancellationToken)
    {
        try
        {
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
                    _logger.LogInformation("🎯 Discovered UAS device: '{DeviceName}' at {IpAddress}:{Port} (FW: {FirmwareVersion})", 
                        device.Name, device.IpAddress, device.Port, device.FirmwareVersion ?? "Unknown");
                }
                else
                {
                    _logger.LogDebug("🔄 UAS device already exists in discovery list: '{DeviceName}' at {IpAddress}:{Port}", 
                        device.Name, device.IpAddress, device.Port);
                }
            }
            finally
            {
                _devicesLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add discovered UAS device: {DeviceName} at {IpAddress}:{Port}", 
                device.Name, device.IpAddress, device.Port);
        }
    }
    
    private List<(IPAddress localIp, IPAddress subnet, IPAddress subnetMask)> GetLocalNetworkInterfaces()
    {
        var networkInterfaces = new List<(IPAddress localIp, IPAddress subnet, IPAddress subnetMask)>();
        
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            
            foreach (var ni in interfaces)
            {
                // Filter for active network interfaces that can connect to UAS devices
                if (ni.OperationalStatus == OperationalStatus.Up && 
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Unknown)
                {
                    // Skip known virtual network interfaces
                    if (IsVirtualNetworkInterface(ni))
                    {
                        _logger.LogDebug("⏭️ Skipping virtual network interface: {InterfaceName} ({Type})", 
                            ni.Name, ni.NetworkInterfaceType);
                        continue;
                    }
                    
                    var ipProperties = ni.GetIPProperties();
                    
                    // Look for IPv4 addresses with valid subnet masks
                    foreach (var unicastAddress in ipProperties.UnicastAddresses)
                    {
                        if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            var localIp = unicastAddress.Address;
                            var subnetMask = unicastAddress.IPv4Mask;
                            
                            // Skip invalid, link-local, or virtual network addresses
                            if (subnetMask != null && !IPAddress.IsLoopback(localIp) && 
                                !localIp.ToString().StartsWith("169.254.") && // Skip APIPA addresses
                                !IsVirtualNetworkAddress(localIp)) // Skip known virtual ranges
                            {
                                var subnet = GetNetworkAddress(localIp, subnetMask);
                                networkInterfaces.Add((localIp, subnet, subnetMask));
                                
                                _logger.LogInformation("🌐 Found physical network interface: {InterfaceName} - Local IP: {LocalIP}, Subnet: {Subnet}/{SubnetMask}", 
                                    ni.Name, localIp, subnet, GetCidrNotation(subnetMask));
                            }
                            else
                            {
                                _logger.LogDebug("⏭️ Skipping virtual IP address: {LocalIP} on {InterfaceName}", 
                                    localIp, ni.Name);
                            }
                        }
                    }
                }
            }
            
            _logger.LogInformation("🔍 Discovered {Count} physical network interfaces for UAS device discovery", networkInterfaces.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate network interfaces for UAS device discovery");
        }
        
        return networkInterfaces;
    }
    
    private static bool IsVirtualNetworkInterface(NetworkInterface networkInterface)
    {
        var name = networkInterface.Name.ToLowerInvariant();
        var description = networkInterface.Description.ToLowerInvariant();
        
        // Known virtual network interface patterns
        var virtualPatterns = new[]
        {
            // Hyper-V virtual switches
            "hyper-v", "vethernet", "hyper", "virtual switch",
            
            // WSL and Docker
            "wsl", "docker", "vmmem",
            
            // VPN adapters
            "vpn", "tap", "tun", "openvpn", "wireguard", "nordvpn", "expressvpn",
            
            // VMware and VirtualBox
            "vmware", "virtualbox", "vbox", "vmnet",
            
            // Other virtual adapters
            "bluetooth network", "teredo tunneling", "6to4 adapter",
            "isatap", "microsoft", "loopback", "pseudo-interface"
        };
        
        return virtualPatterns.Any(pattern => 
            name.Contains(pattern) || description.Contains(pattern));
    }
    
    private static bool IsVirtualNetworkAddress(IPAddress ipAddress)
    {
        var ipString = ipAddress.ToString();
        
        // Known virtual/container network ranges
        var virtualRanges = new[]
        {
            // Docker default ranges
            "172.17.", "172.18.", "172.19.", "172.20.", "172.21.", "172.22.", "172.23.", "172.24.",
            
            // Hyper-V default ranges
            "172.16.", "172.25.", "172.26.", "172.27.", "172.28.", "172.29.", "172.30.", "172.31.",
            
            // WSL ranges
            "172.17.80.", "172.17.81.", "172.17.82.",
            
            // VMware default ranges  
            "172.16.0.", "172.16.1.", "172.16.2.",
            
            // VirtualBox default ranges
            "192.168.56.", "10.0.2."
        };
        
        return virtualRanges.Any(range => ipString.StartsWith(range));
    }
    
    private static IPAddress GetNetworkAddress(IPAddress ipAddress, IPAddress subnetMask)
    {
        var ipBytes = ipAddress.GetAddressBytes();
        var maskBytes = subnetMask.GetAddressBytes();
        var networkBytes = new byte[4];
        
        for (int i = 0; i < 4; i++)
        {
            networkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
        }
        
        return new IPAddress(networkBytes);
    }
    
    private static int GetCidrNotation(IPAddress subnetMask)
    {
        var maskBytes = subnetMask.GetAddressBytes();
        int cidr = 0;
        
        for (int i = 0; i < maskBytes.Length; i++)
        {
            byte b = maskBytes[i];
            while (b > 0)
            {
                cidr++;
                b <<= 1;
            }
        }
        
        return cidr;
    }
    
    private static List<IPAddress> GetHostAddressesInSubnet(IPAddress networkAddress, IPAddress subnetMask)
    {
        var hostAddresses = new List<IPAddress>();
        var networkBytes = networkAddress.GetAddressBytes();
        var maskBytes = subnetMask.GetAddressBytes();
        
        // Calculate the number of host bits
        int hostBits = 32 - GetCidrNotation(subnetMask);
        int totalHosts = (int)Math.Pow(2, hostBits);
        
        // Limit scanning to reasonable subnet sizes to avoid excessive scanning
        // For /16 networks (65536 hosts), this would be too many to scan efficiently
        if (totalHosts > 1024) // Limit to /22 or smaller subnets  
        {
            totalHosts = 1024;
        }
        
        // Generate host addresses (skip network address and broadcast)
        for (int host = 1; host < totalHosts - 1; host++)
        {
            var hostBytes = new byte[4];
            Array.Copy(networkBytes, hostBytes, 4);
            
            // Add the host offset to the network address
            int hostOffset = host;
            for (int i = 3; i >= 0; i--)
            {
                int newValue = hostBytes[i] + (hostOffset & 0xFF);
                hostBytes[i] = (byte)(newValue & 0xFF);
                hostOffset >>= 8;
                
                if (hostOffset == 0) break;
            }
            
            // Ensure we don't exceed subnet boundaries
            var hostAddress = new IPAddress(hostBytes);
            var hostNetwork = GetNetworkAddress(hostAddress, subnetMask);
            
            if (hostNetwork.Equals(networkAddress))
            {
                hostAddresses.Add(hostAddress);
            }
        }
        
        return hostAddresses;
    }
    
    private void ReportProgress(ScanProgress progress)
    {
        try
        {
            ScanProgressChanged?.Invoke(this, progress);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reporting scan progress");
        }
    }
    
    public void Dispose()
    {
        StopScan();
        _scanCts?.Dispose();
        _devicesLock?.Dispose();
    }
}