namespace Inductobot.Models.Commands;

public class CommandRequest
{
    public string CommandId { get; set; } = Guid.NewGuid().ToString();
    public string DeviceId { get; set; } = string.Empty;
    public CommandType Type { get; set; }
    public string Command { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public int TimeoutMs { get; set; } = 5000;
    public int RetryCount { get; set; } = 3;
    public DateTime CreatedAt { get; set; }
    
    public CommandRequest()
    {
        CreatedAt = DateTime.Now;
    }
}

public enum CommandType
{
    Read,
    Write,
    Execute,
    Query,
    Configuration,
    Diagnostic,
    Firmware,
    Custom
}