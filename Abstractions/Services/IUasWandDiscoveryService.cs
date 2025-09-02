using Inductobot.Models.Device;
using Inductobot.Models.Discovery;

namespace Inductobot.Abstractions.Services;

/// <summary>
/// Service for discovering UAS-WAND devices on the network
/// </summary>
public interface IUasWandDiscoveryService
{
    /// <summary>
    /// Collection of discovered devices
    /// </summary>
    IReadOnlyList<UASDeviceInfo> DiscoveredDevices { get; }
    
    /// <summary>
    /// Whether currently scanning for devices
    /// </summary>
    bool IsScanning { get; }
    
    /// <summary>
    /// Device discovered event
    /// </summary>
    event EventHandler<UASDeviceInfo>? DeviceDiscovered;
    
    /// <summary>
    /// Device removed/lost event
    /// </summary>
    event EventHandler<UASDeviceInfo>? DeviceRemoved;
    
    /// <summary>
    /// Scanning state changed event
    /// </summary>
    event EventHandler<bool>? ScanningStateChanged;
    
    /// <summary>
    /// Scan progress changed event
    /// </summary>
    event EventHandler<ScanProgress>? ScanProgressChanged;
    
    /// <summary>
    /// Start scanning for UAS-WAND devices
    /// </summary>
    Task StartScanAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stop scanning for devices
    /// </summary>
    void StopScan();
    
    /// <summary>
    /// Test connection to a specific device
    /// </summary>
    Task<bool> TestConnectionAsync(UASDeviceInfo device, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Force check for running simulator and add to discovered devices
    /// </summary>
    Task ForceDiscoverSimulatorAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get count of event subscribers for debugging
    /// </summary>
    int GetDeviceDiscoveredSubscriberCount();
    
    /// <summary>
    /// Refresh information for a specific device
    /// </summary>
    Task RefreshDeviceAsync(UASDeviceInfo device, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add a device manually to the discovered list
    /// </summary>
    Task<bool> AddDeviceManuallyAsync(string ipAddress, int port, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Remove a device from the discovered list
    /// </summary>
    void RemoveDevice(UASDeviceInfo device);
    
    /// <summary>
    /// Clear all discovered devices
    /// </summary>
    void ClearDevices();
}