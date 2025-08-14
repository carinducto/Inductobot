namespace Inductobot.Models.Commands;

public class ApiResponse<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Extensions { get; set; } = new();
    
    public ApiResponse()
    {
        Timestamp = DateTime.Now;
    }
    
    public static ApiResponse<T> Success(T data, string message = "Success")
    {
        return new ApiResponse<T>
        {
            IsSuccess = true,
            Data = data,
            Message = message
        };
    }
    
    public static ApiResponse<T> Failure(string message, string errorCode = "")
    {
        return new ApiResponse<T>
        {
            IsSuccess = false,
            Message = message,
            ErrorCode = errorCode
        };
    }
}