namespace Inductobot.Models.Device;

/// <summary>
/// Connection health information for UAS-WAND device
/// </summary>
public class ConnectionHealth
{
    public bool IsHealthy { get; set; }
    public TimeSpan ConnectionDuration { get; set; }
    public int PacketsSent { get; set; }
    public int PacketsReceived { get; set; }
    public int PacketsLost { get; set; }
    public TimeSpan? LastResponseTime { get; set; }
    public List<string> Issues { get; set; } = new List<string>();
    
    public double PacketLossRate => PacketsSent > 0 ? (double)PacketsLost / PacketsSent : 0;
}