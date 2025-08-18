using System.Text.Json.Serialization;

namespace Inductobot.Models.Device;

/// <summary>
/// WiFi configuration information from UAS-WAND device
/// </summary>
public class WifiConfiguration
{
    public string? Ssid { get; set; }
    public string? Password { get; set; }
    public bool Enabled { get; set; }
    public int Channel { get; set; }
    public string? IpAddress { get; set; }
}