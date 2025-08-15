namespace Inductobot.Models.Commands;

public class CommandResponse
{
    public string CommandId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
    public ResponseCode Code { get; set; }
    public DateTime ResponseTime { get; set; }
    public double ExecutionTimeMs { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    public CommandResponse()
    {
        ResponseTime = DateTime.Now;
        Code = ResponseCode.Unknown;
    }
    
    public static CommandResponse CreateSuccess(string commandId, object? data = null)
    {
        return new CommandResponse
        {
            CommandId = commandId,
            Success = true,
            Code = ResponseCode.Success,
            Data = data,
            Message = "Command executed successfully"
        };
    }
    
    public static CommandResponse CreateError(string commandId, string message, ResponseCode code = ResponseCode.Error)
    {
        return new CommandResponse
        {
            CommandId = commandId,
            Success = false,
            Code = code,
            Message = message
        };
    }
}

public enum ResponseCode
{
    Success = 200,
    PartialSuccess = 206,
    BadRequest = 400,
    Unauthorized = 401,
    NotFound = 404,
    Timeout = 408,
    Error = 500,
    NotImplemented = 501,
    ServiceUnavailable = 503,
    ConnectionError = 521, // Custom code for connection errors
    Unknown = 0
}