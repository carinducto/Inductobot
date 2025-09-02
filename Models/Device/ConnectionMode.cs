namespace Inductobot.Models.Device;

/// <summary>
/// Defines the connection mode for UAS-WAND devices
/// </summary>
public enum ConnectionMode
{
    /// <summary>
    /// No active connection mode selected
    /// </summary>
    None,
    
    /// <summary>
    /// WiFi/HTTP connection mode for network communication
    /// </summary>
    WiFiHttp,
    
    /// <summary>
    /// USB/COM port connection mode for serial communication
    /// </summary>
    UsbComPort
}

/// <summary>
/// Connection mode information
/// </summary>
public class ConnectionModeInfo
{
    public ConnectionMode Mode { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    
    public static ConnectionModeInfo WiFiMode => new()
    {
        Mode = ConnectionMode.WiFiHttp,
        DisplayName = "WiFi/Network",
        Description = "Connect to devices over WiFi or Ethernet network",
        IconGlyph = "📡"
    };
    
    public static ConnectionModeInfo UsbMode => new()
    {
        Mode = ConnectionMode.UsbComPort,
        DisplayName = "USB/Serial",
        Description = "Connect to devices via USB cable (COM port)",
        IconGlyph = "🔌"
    };
}