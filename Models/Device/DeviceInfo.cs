namespace Inductobot.Models.Device;

public class UASDeviceInfo
{
    public string DeviceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; } = 502; // Default Modbus TCP port
    public string? FirmwareVersion { get; set; }
    public string? SerialNumber { get; set; }
    public DeviceType Type { get; set; }
    public DateTime LastConnected { get; set; }
    public DateTime LastSeen { get; set; }
    public bool IsOnline { get; set; }
    public Dictionary<string, object> CustomProperties { get; set; } = new();
    
    // Additional property for UI display purposes
    public string? DeviceType { get; set; }
}

public enum DeviceType
{
    WandV3,
    Simulator,
    Custom
}