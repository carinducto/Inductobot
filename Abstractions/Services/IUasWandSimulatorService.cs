namespace Inductobot.Abstractions.Services;

/// <summary>
/// Interface for controlling the UAS-WAND device simulator
/// </summary>
public interface IUasWandSimulatorService
{
    /// <summary>
    /// Whether the simulator is currently running
    /// </summary>
    bool IsRunning { get; }
    
    /// <summary>
    /// The port the simulator is listening on
    /// </summary>
    int Port { get; }
    
    /// <summary>
    /// Local IP address of the simulator
    /// </summary>
    string? IPAddress { get; }
    
    /// <summary>
    /// Event fired when simulator state changes
    /// </summary>
    event EventHandler<bool>? SimulatorStateChanged;
    
    /// <summary>
    /// Start the simulator if not already running
    /// </summary>
    Task<bool> StartSimulatorAsync();
    
    /// <summary>
    /// Stop the simulator if running
    /// </summary>
    Task<bool> StopSimulatorAsync();
    
    /// <summary>
    /// Get simulator status information
    /// </summary>
    SimulatorStatus GetStatus();
}

/// <summary>
/// Status information about the simulator
/// </summary>
public class SimulatorStatus
{
    public bool IsRunning { get; set; }
    public int Port { get; set; }
    public string? IPAddress { get; set; }
    public string DeviceName { get; set; } = "UAS-WAND_Simulator";
    public string FirmwareVersion { get; set; } = "3.9.0-sim";
    public int ConnectedClients { get; set; }
    public DateTime? StartTime { get; set; }
    public string Status { get; set; } = "Stopped";
}