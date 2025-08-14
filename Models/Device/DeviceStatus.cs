namespace Inductobot.Models.Device;

public class DeviceStatus
{
    public string DeviceId { get; set; } = string.Empty;
    public ConnectionState ConnectionState { get; set; }
    public DateTime LastUpdate { get; set; }
    public int SignalStrength { get; set; } // 0-100
    public double ResponseTime { get; set; } // in milliseconds
    public string StatusMessage { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public Dictionary<string, double> Metrics { get; set; } = new();
    
    public DeviceStatus()
    {
        LastUpdate = DateTime.Now;
        ConnectionState = ConnectionState.Disconnected;
    }
}