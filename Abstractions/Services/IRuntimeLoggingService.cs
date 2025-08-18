using Microsoft.Extensions.Logging;

namespace Inductobot.Abstractions.Services;

/// <summary>
/// Service for managing runtime logging configuration changes
/// </summary>
public interface IRuntimeLoggingService
{
    /// <summary>
    /// Current minimum log level
    /// </summary>
    LogLevel MinimumLogLevel { get; }
    
    /// <summary>
    /// Whether file logging is enabled
    /// </summary>
    bool IsFileLoggingEnabled { get; }
    
    /// <summary>
    /// Maximum number of log files to keep
    /// </summary>
    int MaxLogFiles { get; }
    
    /// <summary>
    /// Event fired when logging configuration changes
    /// </summary>
    event EventHandler<LoggingConfigurationChangedEventArgs>? LoggingConfigurationChanged;
    
    /// <summary>
    /// Update logging configuration at runtime
    /// </summary>
    void UpdateLoggingConfiguration(LogLevel minLevel, bool enableFileLogging, int maxLogFiles);
    
    /// <summary>
    /// Apply configuration from the configuration service
    /// </summary>
    void ApplyConfiguration(IConfigurationService config);
}

/// <summary>
/// Event args for logging configuration changes
/// </summary>
public class LoggingConfigurationChangedEventArgs : EventArgs
{
    public LogLevel NewMinimumLevel { get; }
    public bool FileLoggingEnabled { get; }
    public int MaxLogFiles { get; }
    
    public LoggingConfigurationChangedEventArgs(LogLevel newMinimumLevel, bool fileLoggingEnabled, int maxLogFiles)
    {
        NewMinimumLevel = newMinimumLevel;
        FileLoggingEnabled = fileLoggingEnabled;
        MaxLogFiles = maxLogFiles;
    }
}