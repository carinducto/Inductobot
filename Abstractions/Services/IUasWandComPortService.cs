using Inductobot.Models.Device;

namespace Inductobot.Abstractions.Services;

/// <summary>
/// Service for managing UAS-WAND USB/COM port communication
/// </summary>
public interface IUasWandComPortService
{
    /// <summary>
    /// List of available COM ports
    /// </summary>
    IReadOnlyList<ComPortInfo> AvailableComPorts { get; }
    
    /// <summary>
    /// Currently connected COM port
    /// </summary>
    ComPortInfo? ConnectedPort { get; }
    
    /// <summary>
    /// Whether a COM port is currently connected
    /// </summary>
    bool IsConnected { get; }
    
    /// <summary>
    /// COM port discovered event
    /// </summary>
    event EventHandler<ComPortInfo>? ComPortDiscovered;
    
    /// <summary>
    /// COM port removed event
    /// </summary>
    event EventHandler<ComPortInfo>? ComPortRemoved;
    
    /// <summary>
    /// Connection state changed event
    /// </summary>
    event EventHandler<bool>? ConnectionStateChanged;
    
    /// <summary>
    /// Scan progress changed event
    /// </summary>
    event EventHandler<(int current, int total, string status)>? ScanProgressChanged;
    
    /// <summary>
    /// Scan for UAS-WAND USB devices and their virtual COM ports
    /// </summary>
    Task<IReadOnlyList<ComPortInfo>> ScanForUasComPortsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Connect to a specific COM port
    /// </summary>
    Task<bool> ConnectAsync(string portName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Disconnect from current COM port
    /// </summary>
    Task DisconnectAsync();
    
    /// <summary>
    /// Send command to the connected COM port
    /// </summary>
    Task<string?> SendCommandAsync(string command, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Configure device settings via COM port
    /// </summary>
    Task<bool> ConfigureDeviceAsync(DeviceConfiguration config, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Read device configuration from COM port
    /// </summary>
    Task<DeviceConfiguration?> ReadConfigurationAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a COM port is a UAS device
    /// </summary>
    Task<bool> IsUasDeviceAsync(string portName, CancellationToken cancellationToken = default);
}