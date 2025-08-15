namespace Inductobot.Models.Commands;

/// <summary>
/// Scan status information from UAS-WAND device
/// </summary>
public class ScanStatus
{
    public int Status { get; set; }
    public string? Message { get; set; }
    public int Progress { get; set; }
    public int TotalPoints { get; set; }
}