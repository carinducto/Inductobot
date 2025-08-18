using Microsoft.Extensions.Logging;

namespace Inductobot.Abstractions.Services;

/// <summary>
/// Configuration service for managing application settings
/// </summary>
public interface IConfigurationService
{
    // Connection Settings
    int ConnectionTimeoutSeconds { get; }
    int RetryAttempts { get; }
    int KeepAliveIntervalSeconds { get; }
    bool AutoReconnect { get; }
    
    // Discovery Settings  
    int ScanTimeoutMinutes { get; }
    int[] DefaultPorts { get; }
    bool AutoDiscovery { get; }
    
    // UI Settings
    string Theme { get; }
    bool ShowTimestamps { get; }
    bool ShowDebugInfo { get; }
    
    // Logging Settings
    LogLevel LogLevel { get; }
    bool FileLogging { get; }
    int MaxLogFiles { get; }
    
    /// <summary>
    /// Event fired when configuration changes
    /// </summary>
    event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;
    
    /// <summary>
    /// Refresh configuration from storage
    /// </summary>
    void RefreshConfiguration();
    
    /// <summary>
    /// Get timeout as TimeSpan for connection operations
    /// </summary>
    TimeSpan GetConnectionTimeout();
    
    /// <summary>
    /// Get timeout as TimeSpan for keep-alive operations
    /// </summary>
    TimeSpan GetKeepAliveInterval();
    
    /// <summary>
    /// Get timeout as TimeSpan for scan operations
    /// </summary>
    TimeSpan GetScanTimeout();
}

/// <summary>
/// Event args for configuration changes
/// </summary>
public class ConfigurationChangedEventArgs : EventArgs
{
    public string[] ChangedSettings { get; }
    
    public ConfigurationChangedEventArgs(params string[] changedSettings)
    {
        ChangedSettings = changedSettings;
    }
}