using Inductobot.Abstractions.Services;
using Inductobot.Models.Device;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.IO.Ports;
using System.Management;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Inductobot.Services.Communication;

/// <summary>
/// Service for managing UAS-WAND USB/COM port communication
/// </summary>
public class UasWandComPortService : IUasWandComPortService, IDisposable
{
    private readonly ILogger<UasWandComPortService> _logger;
    private readonly List<ComPortInfo> _availableComPorts = new();
    private SerialPort? _serialPort;
    private readonly SemaphoreSlim _portLock = new(1, 1);
    
    // UAS-WAND USB identifiers
    private const string UAS_VENDOR_ID = "064B"; // Example VID - replace with actual
    private const string UAS_PRODUCT_ID = "0006"; // Example PID - replace with actual
    private const int DEFAULT_BAUD_RATE = 115200;
    private const int READ_TIMEOUT = 5000;
    private const int WRITE_TIMEOUT = 5000;
    
    public IReadOnlyList<ComPortInfo> AvailableComPorts => _availableComPorts.AsReadOnly();
    public ComPortInfo? ConnectedPort { get; private set; }
    public bool IsConnected => _serialPort?.IsOpen ?? false;
    
    public event EventHandler<ComPortInfo>? ComPortDiscovered;
    public event EventHandler<ComPortInfo>? ComPortRemoved;
    public event EventHandler<bool>? ConnectionStateChanged;
    public event EventHandler<(int current, int total, string status)>? ScanProgressChanged;
    
    public UasWandComPortService(ILogger<UasWandComPortService> logger)
    {
        _logger = logger;
    }
    
    public async Task<IReadOnlyList<ComPortInfo>> ScanForUasComPortsAsync(CancellationToken cancellationToken = default)
    {
        await _portLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Scanning for UAS-WAND COM ports...");
            
            var previousPorts = new List<ComPortInfo>(_availableComPorts);
            _availableComPorts.Clear();
            
            // Get all available COM ports
            var portNames = SerialPort.GetPortNames();
            var totalPorts = portNames.Length;
            
            ScanProgressChanged?.Invoke(this, (0, totalPorts, "Starting COM port scan..."));
            
            for (int i = 0; i < portNames.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                
                var portName = portNames[i];
                ScanProgressChanged?.Invoke(this, (i + 1, totalPorts, $"Scanning {portName}..."));
                
                var portInfo = await GetComPortInfoAsync(portName, cancellationToken);
                if (portInfo != null)
                {
                    _availableComPorts.Add(portInfo);
                    
                    // Check if this is a new port
                    if (!previousPorts.Any(p => p.PortName == portInfo.PortName))
                    {
                        _logger.LogInformation("New COM port discovered: {PortName} - {Description}", 
                            portInfo.PortName, portInfo.Description);
                        ComPortDiscovered?.Invoke(this, portInfo);
                    }
                }
            }
            
            ScanProgressChanged?.Invoke(this, (totalPorts, totalPorts, "Scan complete"));
            
            // Check for removed ports
            foreach (var oldPort in previousPorts)
            {
                if (!_availableComPorts.Any(p => p.PortName == oldPort.PortName))
                {
                    _logger.LogInformation("COM port removed: {PortName}", oldPort.PortName);
                    ComPortRemoved?.Invoke(this, oldPort);
                }
            }
            
            _logger.LogInformation("Found {Count} COM ports ({UasCount} UAS devices)", 
                _availableComPorts.Count, _availableComPorts.Count(p => p.IsUasDevice));
            
            return AvailableComPorts;
        }
        finally
        {
            _portLock.Release();
        }
    }
    
    private async Task<ComPortInfo?> GetComPortInfoAsync(string portName, CancellationToken cancellationToken)
    {
        try
        {
            var portInfo = new ComPortInfo
            {
                PortName = portName,
                BaudRate = DEFAULT_BAUD_RATE
            };
            
            // Try to get additional info via WMI
            await Task.Run(() =>
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        "SELECT * FROM Win32_PnPEntity WHERE Caption LIKE '%" + portName + "%'");
                    
                    foreach (ManagementObject queryObj in searcher.Get())
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;
                        
                        portInfo.Description = queryObj["Caption"]?.ToString() ?? portName;
                        var deviceId = queryObj["DeviceID"]?.ToString() ?? "";
                        var pnpDeviceId = queryObj["PNPDeviceID"]?.ToString() ?? "";
                        
                        // Extract VID/PID from device ID
                        var vidMatch = Regex.Match(deviceId + pnpDeviceId, @"VID_([0-9A-F]{4})", RegexOptions.IgnoreCase);
                        var pidMatch = Regex.Match(deviceId + pnpDeviceId, @"PID_([0-9A-F]{4})", RegexOptions.IgnoreCase);
                        
                        if (vidMatch.Success)
                            portInfo.VendorId = vidMatch.Groups[1].Value;
                        if (pidMatch.Success)
                            portInfo.ProductId = pidMatch.Groups[1].Value;
                        
                        portInfo.Manufacturer = queryObj["Manufacturer"]?.ToString();
                        portInfo.HardwareId = deviceId;
                        
                        // Check if this is a UAS device
                        portInfo.IsUasDevice = IsUasDevice(portInfo);
                        
                        break; // We found the info for this port
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get WMI info for port {PortName}", portName);
                }
            }, cancellationToken);
            
            // If we couldn't get info from WMI, still return basic info
            if (string.IsNullOrEmpty(portInfo.Description))
            {
                portInfo.Description = $"Serial Port ({portName})";
            }
            
            // Check if port is available (not in use)
            portInfo.IsAvailable = await IsPortAvailableAsync(portName, cancellationToken);
            
            return portInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get info for COM port {PortName}", portName);
            return null;
        }
    }
    
    private bool IsUasDevice(ComPortInfo portInfo)
    {
        // Check by VID/PID
        if (portInfo.VendorId?.Equals(UAS_VENDOR_ID, StringComparison.OrdinalIgnoreCase) == true &&
            portInfo.ProductId?.Equals(UAS_PRODUCT_ID, StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }
        
        // Check by description/name
        var description = portInfo.Description?.ToLower() ?? "";
        var manufacturer = portInfo.Manufacturer?.ToLower() ?? "";
        
        return description.Contains("uas") || 
               description.Contains("wand") ||
               manufacturer.Contains("inductosense") ||
               manufacturer.Contains("uas");
    }
    
    private async Task<bool> IsPortAvailableAsync(string portName, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var testPort = new SerialPort(portName);
                testPort.Open();
                testPort.Close();
                return true;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }
    
    public async Task<bool> ConnectAsync(string portName, CancellationToken cancellationToken = default)
    {
        await _portLock.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected)
            {
                await DisconnectInternalAsync();
            }
            
            _logger.LogInformation("Connecting to COM port {PortName}...", portName);
            
            _serialPort = new SerialPort(portName)
            {
                BaudRate = DEFAULT_BAUD_RATE,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                ReadTimeout = READ_TIMEOUT,
                WriteTimeout = WRITE_TIMEOUT
            };
            
            _serialPort.Open();
            
            // Find the port info
            ConnectedPort = _availableComPorts.FirstOrDefault(p => p.PortName == portName);
            if (ConnectedPort == null)
            {
                // Create basic port info if not in list
                ConnectedPort = new ComPortInfo
                {
                    PortName = portName,
                    Description = $"Serial Port ({portName})",
                    BaudRate = DEFAULT_BAUD_RATE
                };
            }
            
            _logger.LogInformation("Successfully connected to COM port {PortName}", portName);
            ConnectionStateChanged?.Invoke(this, true);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to COM port {PortName}", portName);
            _serialPort?.Dispose();
            _serialPort = null;
            ConnectedPort = null;
            return false;
        }
        finally
        {
            _portLock.Release();
        }
    }
    
    public async Task DisconnectAsync()
    {
        await _portLock.WaitAsync();
        try
        {
            await DisconnectInternalAsync();
        }
        finally
        {
            _portLock.Release();
        }
    }
    
    private Task DisconnectInternalAsync()
    {
        if (_serialPort?.IsOpen == true)
        {
            try
            {
                _serialPort.Close();
                _logger.LogInformation("Disconnected from COM port {PortName}", ConnectedPort?.PortName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing COM port");
            }
        }
        
        _serialPort?.Dispose();
        _serialPort = null;
        ConnectedPort = null;
        ConnectionStateChanged?.Invoke(this, false);
        
        return Task.CompletedTask;
    }
    
    public async Task<string?> SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _serialPort == null)
        {
            _logger.LogWarning("Cannot send command - not connected to COM port");
            return null;
        }
        
        await _portLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogDebug("Sending command to COM port: {Command}", command);
            
            // Clear any existing data
            _serialPort.DiscardInBuffer();
            _serialPort.DiscardOutBuffer();
            
            // Send command
            await Task.Run(() => _serialPort.WriteLine(command), cancellationToken);
            
            // Read response
            var response = await Task.Run(() =>
            {
                try
                {
                    return _serialPort.ReadLine();
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning("Timeout reading response from COM port");
                    return null;
                }
            }, cancellationToken);
            
            _logger.LogDebug("Received response from COM port: {Response}", response);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending command to COM port");
            return null;
        }
        finally
        {
            _portLock.Release();
        }
    }
    
    public async Task<bool> ConfigureDeviceAsync(DeviceConfiguration config, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Configuring device via COM port...");
            
            // Convert configuration to JSON or command format
            var configJson = JsonSerializer.Serialize(config);
            var response = await SendCommandAsync($"CONFIG:{configJson}", cancellationToken);
            
            if (response?.Contains("OK") == true || response?.Contains("SUCCESS") == true)
            {
                _logger.LogInformation("Device configuration successful");
                return true;
            }
            
            _logger.LogWarning("Device configuration failed: {Response}", response);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure device");
            return false;
        }
    }
    
    public async Task<DeviceConfiguration?> ReadConfigurationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Reading device configuration via COM port...");
            
            var response = await SendCommandAsync("GET_CONFIG", cancellationToken);
            if (string.IsNullOrEmpty(response))
            {
                _logger.LogWarning("No response received when reading configuration");
                return null;
            }
            
            // Parse response (assuming JSON format)
            try
            {
                var config = JsonSerializer.Deserialize<DeviceConfiguration>(response);
                _logger.LogInformation("Successfully read device configuration");
                return config;
            }
            catch
            {
                // If not JSON, create a basic config from the response
                _logger.LogWarning("Could not parse configuration response as JSON");
                return new DeviceConfiguration
                {
                    DeviceName = ConnectedPort?.Description ?? "Unknown"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read device configuration");
            return null;
        }
    }
    
    public async Task<bool> IsUasDeviceAsync(string portName, CancellationToken cancellationToken = default)
    {
        try
        {
            // First check if we already have info about this port
            var portInfo = _availableComPorts.FirstOrDefault(p => p.PortName == portName);
            if (portInfo != null)
            {
                return portInfo.IsUasDevice;
            }
            
            // Try to connect and query the device
            var wasConnected = IsConnected;
            var previousPort = ConnectedPort;
            
            if (await ConnectAsync(portName, cancellationToken))
            {
                // Send identification command
                var response = await SendCommandAsync("ID", cancellationToken);
                
                // Restore previous connection if needed
                if (!wasConnected)
                {
                    await DisconnectAsync();
                }
                else if (previousPort != null && previousPort.PortName != portName)
                {
                    await ConnectAsync(previousPort.PortName, cancellationToken);
                }
                
                // Check if response indicates UAS device
                if (!string.IsNullOrEmpty(response))
                {
                    var lowerResponse = response.ToLower();
                    return lowerResponse.Contains("uas") || 
                           lowerResponse.Contains("wand") ||
                           lowerResponse.Contains("inductosense");
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if {PortName} is a UAS device", portName);
            return false;
        }
    }
    
    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        _portLock?.Dispose();
    }
}