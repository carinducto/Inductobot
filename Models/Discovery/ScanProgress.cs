namespace Inductobot.Models.Discovery;

/// <summary>
/// Progress information for network device scanning operations
/// </summary>
public class ScanProgress
{
    /// <summary>
    /// Current step description
    /// </summary>
    public string CurrentStep { get; set; } = string.Empty;
    
    /// <summary>
    /// Percentage complete (0-100)
    /// </summary>
    public int PercentComplete { get; set; }
    
    /// <summary>
    /// Number of subnets scanned
    /// </summary>
    public int SubnetsScanned { get; set; }
    
    /// <summary>
    /// Total number of subnets to scan
    /// </summary>
    public int TotalSubnets { get; set; }
    
    /// <summary>
    /// Number of hosts scanned in current subnet
    /// </summary>
    public int HostsScanned { get; set; }
    
    /// <summary>
    /// Total number of hosts to scan in current subnet
    /// </summary>
    public int TotalHosts { get; set; }
    
    /// <summary>
    /// Number of devices found (UAS and generic)
    /// </summary>
    public int UasDevicesFound { get; set; }
    
    /// <summary>
    /// Additional debug or status information
    /// </summary>
    public string? DebugInfo { get; set; }
    
    /// <summary>
    /// Current subnet being scanned
    /// </summary>
    public string? CurrentSubnet { get; set; }
}