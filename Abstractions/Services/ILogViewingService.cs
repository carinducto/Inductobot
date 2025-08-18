using Microsoft.Extensions.Logging;

namespace Inductobot.Abstractions.Services;

/// <summary>
/// Service for viewing and managing log files
/// </summary>
public interface ILogViewingService
{
    /// <summary>
    /// Get all available log files
    /// </summary>
    Task<LogFileInfo[]> GetLogFilesAsync();
    
    /// <summary>
    /// Read log file contents with optional filtering
    /// </summary>
    Task<LogEntry[]> ReadLogFileAsync(string filePath, LogLevel? minLevel = null, string? searchText = null, int maxEntries = 1000);
    
    /// <summary>
    /// Get recent log entries from all files combined
    /// </summary>
    Task<LogEntry[]> GetRecentEntriesAsync(int maxEntries = 100, LogLevel? minLevel = null);
    
    /// <summary>
    /// Delete a specific log file
    /// </summary>
    Task<bool> DeleteLogFileAsync(string filePath);
    
    /// <summary>
    /// Export logs to a file
    /// </summary>
    Task<string?> ExportLogsAsync(LogEntry[] entries, string format = "txt");
}

/// <summary>
/// Information about a log file
/// </summary>
public class LogFileInfo
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public long SizeBytes { get; set; }
    public int ApproximateEntryCount { get; set; }
    
    public string FormattedSize => FormatBytes(SizeBytes);
    public string FormattedAge => FormatTimeSpan(DateTime.Now - ModifiedAt);
    
    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024 * 1024):F1} MB",
            _ => $"{bytes / (1024 * 1024 * 1024):F1} GB"
        };
    }
    
    private static string FormatTimeSpan(TimeSpan timeSpan)
    {
        return timeSpan.TotalDays switch
        {
            < 1 when timeSpan.TotalHours < 1 => $"{timeSpan.Minutes}m ago",
            < 1 => $"{timeSpan.Hours}h {timeSpan.Minutes}m ago",
            < 30 => $"{(int)timeSpan.TotalDays}d ago",
            < 365 => $"{(int)(timeSpan.TotalDays / 30)}mo ago",
            _ => $"{(int)(timeSpan.TotalDays / 365)}y ago"
        };
    }
}

/// <summary>
/// A single log entry
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Category { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Exception { get; set; }
    public string SourceFile { get; set; } = "";
    
    public string FormattedLevel => Level.ToString().ToUpper().PadRight(5);
    public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
    public string FormattedCategory => Category.Length > 30 ? $"{Category[..27]}..." : Category;
    
    public string LevelColor => Level switch
    {
        LogLevel.Critical => "Red",
        LogLevel.Error => "DarkRed", 
        LogLevel.Warning => "Orange",
        LogLevel.Information => "Blue",
        LogLevel.Debug => "Gray",
        LogLevel.Trace => "LightGray",
        _ => "Black"
    };
}