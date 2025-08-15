using Inductobot.Models.Device;

namespace Inductobot.Abstractions.Communication;

/// <summary>
/// Abstract interface for UAS-WAND device communication transport layers
/// </summary>
public interface IUasWandTransport : IDisposable
{
    /// <summary>
    /// Current connection state
    /// </summary>
    ConnectionState ConnectionState { get; }
    
    /// <summary>
    /// Whether the transport is currently connected
    /// </summary>
    bool IsConnected { get; }
    
    /// <summary>
    /// Currently connected UAS-WAND device information
    /// </summary>
    UASDeviceInfo? CurrentDevice { get; }
    
    /// <summary>
    /// Connection state changed event
    /// </summary>
    event EventHandler<ConnectionState>? ConnectionStateChanged;
    
    /// <summary>
    /// Connect to a UAS-WAND device by IP and port
    /// </summary>
    Task<bool> ConnectAsync(string address, int port, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Connect to a UAS-WAND device using device info
    /// </summary>
    Task<bool> ConnectAsync(UASDeviceInfo device, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Disconnect from the current UAS-WAND device
    /// </summary>
    Task DisconnectAsync();
    
    /// <summary>
    /// Send raw binary data to the UAS-WAND device
    /// </summary>
    Task<byte[]> SendRawDataAsync(byte[] data, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Send a text command to the UAS-WAND device
    /// </summary>
    Task<string> SendCommandAsync(string command, CancellationToken cancellationToken = default);
}