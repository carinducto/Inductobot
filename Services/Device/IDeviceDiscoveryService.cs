using Inductobot.Models.Device;

namespace Inductobot.Services.Device;

public interface IDeviceDiscoveryService
{
    event EventHandler<UASDeviceInfo>? DeviceDiscovered;
    event EventHandler<UASDeviceInfo>? DeviceRemoved;
    event EventHandler<bool>? ScanningStateChanged;
    
    IReadOnlyList<UASDeviceInfo> DiscoveredDevices { get; }
    bool IsScanning { get; }
    
    Task StartScanAsync(CancellationToken cancellationToken = default);
    void StopScan();
    Task<bool> TestConnectionAsync(UASDeviceInfo device, CancellationToken cancellationToken = default);
    Task RefreshDeviceAsync(UASDeviceInfo device, CancellationToken cancellationToken = default);
    void AddManualDevice(string ipAddress, int port);
    void RemoveDevice(UASDeviceInfo device);
}