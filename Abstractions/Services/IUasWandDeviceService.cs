using Inductobot.Models.Device;

namespace Inductobot.Abstractions.Services;

/// <summary>
/// Business logic service for UAS-WAND device management
/// </summary>
public interface IUasWandDeviceService
{
    /// <summary>
    /// Current connection state
    /// </summary>
    ConnectionState ConnectionState { get; }
    
    /// <summary>
    /// Whether currently connected to a device
    /// </summary>
    bool IsConnected { get; }
    
    /// <summary>
    /// Currently connected device information
    /// </summary>
    UASDeviceInfo? CurrentDevice { get; }
    
    /// <summary>
    /// Connection state changed event
    /// </summary>
    event EventHandler<ConnectionState>? ConnectionStateChanged;
    
    /// <summary>
    /// Connect to a UAS-WAND device
    /// </summary>
    Task<bool> ConnectToDeviceAsync(string ipAddress, int port, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Connect to a UAS-WAND device
    /// </summary>
    Task<bool> ConnectToDeviceAsync(UASDeviceInfo device, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Disconnect from current device
    /// </summary>
    Task DisconnectAsync();
    
    /// <summary>
    /// Test connection to a device without connecting
    /// </summary>
    Task<bool> TestConnectionAsync(string ipAddress, int port, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get connection health information
    /// </summary>
    Task<ConnectionHealth> GetConnectionHealthAsync();
}

