namespace Inductobot.Models.Device;

/// <summary>
/// Information about a COM port
/// </summary>
public class ComPortInfo
{
    /// <summary>
    /// COM port name (e.g., "COM3")
    /// </summary>
    public string PortName { get; set; } = string.Empty;
    
    /// <summary>
    /// Device description
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// USB Vendor ID (if applicable)
    /// </summary>
    public string? VendorId { get; set; }
    
    /// <summary>
    /// USB Product ID (if applicable)
    /// </summary>
    public string? ProductId { get; set; }
    
    /// <summary>
    /// Device serial number
    /// </summary>
    public string? SerialNumber { get; set; }
    
    /// <summary>
    /// Whether this is a UAS-WAND device
    /// </summary>
    public bool IsUasDevice { get; set; }
    
    /// <summary>
    /// Device manufacturer
    /// </summary>
    public string? Manufacturer { get; set; }
    
    /// <summary>
    /// Hardware ID
    /// </summary>
    public string? HardwareId { get; set; }
    
    /// <summary>
    /// Baud rate for communication
    /// </summary>
    public int BaudRate { get; set; } = 115200;
    
    /// <summary>
    /// Whether the port is currently available
    /// </summary>
    public bool IsAvailable { get; set; } = true;
    
    /// <summary>
    /// Last seen timestamp
    /// </summary>
    public DateTime LastSeen { get; set; } = DateTime.Now;
}