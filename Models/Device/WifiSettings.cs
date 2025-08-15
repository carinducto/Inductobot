namespace Inductobot.Models.Device;

/// <summary>
/// WiFi settings to be applied to UAS-WAND device
/// </summary>
public class WifiSettings
{
    public string? Ssid { get; set; }
    public string? Password { get; set; }
    public bool Enable { get; set; }
}