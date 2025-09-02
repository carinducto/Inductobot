using Inductobot.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace Inductobot.Services.Core;

/// <summary>
/// Configuration service implementation that reads from MAUI Preferences
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    
    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

    public ConfigurationService(ILogger<ConfigurationService> logger)
    {
        _logger = logger;
        _logger.LogInformation("ConfigurationService initialized");
    }

    // Connection Settings
    public int ConnectionTimeoutSeconds => GetIntSetting("ConnectionTimeout", 15);
    public int RetryAttempts => GetIntSetting("RetryAttempts", 3);
    public int KeepAliveIntervalSeconds => GetIntSetting("KeepAliveInterval", 30);
    public bool AutoReconnect => Preferences.Get("AutoReconnect", true);

    // Discovery Settings
    public int ScanTimeoutMinutes => GetIntSetting("ScanTimeout", 2);
    public int[] DefaultPorts => ParsePorts(Preferences.Get("DefaultPorts", "80,8080,5000,443,8443")); // HTTP ports first due to ESP32 memory constraints
    public bool AutoDiscovery => Preferences.Get("AutoDiscovery", false);

    // UI Settings
    public string Theme => Preferences.Get("Theme", "System");
    public bool ShowTimestamps => Preferences.Get("ShowTimestamps", true);
    public bool ShowDebugInfo => Preferences.Get("ShowDebugInfo", false);

    // Logging Settings
    public LogLevel LogLevel => ParseLogLevel(Preferences.Get("LogLevel", "Information"));
    public bool FileLogging => Preferences.Get("FileLogging", true);
    public int MaxLogFiles => GetIntSetting("MaxLogFiles", 10);

    public TimeSpan GetConnectionTimeout() => TimeSpan.FromSeconds(ConnectionTimeoutSeconds);
    public TimeSpan GetKeepAliveInterval() => TimeSpan.FromSeconds(KeepAliveIntervalSeconds);
    public TimeSpan GetScanTimeout() => TimeSpan.FromMinutes(ScanTimeoutMinutes);

    public void RefreshConfiguration()
    {
        _logger.LogInformation("Configuration refreshed from preferences");
        ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs("All"));
    }

    private int GetIntSetting(string key, int defaultValue)
    {
        try
        {
            var value = Preferences.Get(key, defaultValue.ToString());
            if (int.TryParse(value, out var intValue))
            {
                return Math.Max(1, intValue); // Ensure positive values
            }
            _logger.LogWarning("Invalid integer value for setting {Key}: {Value}, using default {Default}", 
                key, value, defaultValue);
            return defaultValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading setting {Key}, using default {Default}", key, defaultValue);
            return defaultValue;
        }
    }

    private int[] ParsePorts(string portsString)
    {
        try
        {
            var ports = portsString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => int.TryParse(p, out var port) && port > 0 && port <= 65535)
                .Select(int.Parse)
                .ToArray();

            return ports.Length > 0 ? ports : new[] { 80, 8080, 5000, 443, 8443 }; // HTTP ports first
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing ports string '{PortsString}', using defaults", portsString);
            return new[] { 80, 8080, 5000, 443, 8443 }; // HTTP ports first due to ESP32 SSL memory issues
        }
    }

    private LogLevel ParseLogLevel(string logLevelString)
    {
        try
        {
            return Enum.TryParse<LogLevel>(logLevelString, true, out var level) 
                ? level 
                : LogLevel.Information;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing log level '{LogLevel}', using Information", logLevelString);
            return LogLevel.Information;
        }
    }
}