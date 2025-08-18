using Inductobot.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace Inductobot.Services.Core;

/// <summary>
/// Runtime logging configuration service implementation
/// </summary>
public class RuntimeLoggingService : IRuntimeLoggingService, IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<RuntimeLoggingService> _logger;
    private readonly IConfigurationService _config;
    
    public LogLevel MinimumLogLevel { get; private set; } = LogLevel.Information;
    public bool IsFileLoggingEnabled { get; private set; } = true;
    public int MaxLogFiles { get; private set; } = 10;
    
    public event EventHandler<LoggingConfigurationChangedEventArgs>? LoggingConfigurationChanged;

    public RuntimeLoggingService(ILoggerFactory loggerFactory, IConfigurationService config)
    {
        _loggerFactory = loggerFactory;
        _config = config;
        _logger = _loggerFactory.CreateLogger<RuntimeLoggingService>();
        
        // Initialize from configuration
        ApplyConfiguration(_config);
        
        // Subscribe to configuration changes
        _config.ConfigurationChanged += OnConfigurationChanged;
        
        _logger.LogInformation("RuntimeLoggingService initialized with LogLevel: {LogLevel}, FileLogging: {FileLogging}, MaxFiles: {MaxFiles}", 
            MinimumLogLevel, IsFileLoggingEnabled, MaxLogFiles);
    }

    public void UpdateLoggingConfiguration(LogLevel minLevel, bool enableFileLogging, int maxLogFiles)
    {
        var oldLevel = MinimumLogLevel;
        var oldFileLogging = IsFileLoggingEnabled;
        var oldMaxFiles = MaxLogFiles;
        
        MinimumLogLevel = minLevel;
        IsFileLoggingEnabled = enableFileLogging;
        MaxLogFiles = Math.Max(1, maxLogFiles); // Ensure at least 1 file
        
        _logger.LogInformation("Logging configuration updated: Level {OldLevel}→{NewLevel}, FileLogging {OldFileLogging}→{NewFileLogging}, MaxFiles {OldMaxFiles}→{NewMaxFiles}",
            oldLevel, MinimumLogLevel, oldFileLogging, IsFileLoggingEnabled, oldMaxFiles, MaxLogFiles);
        
        // Fire event to notify other services
        LoggingConfigurationChanged?.Invoke(this, new LoggingConfigurationChangedEventArgs(MinimumLogLevel, IsFileLoggingEnabled, MaxLogFiles));
    }

    public void ApplyConfiguration(IConfigurationService config)
    {
        UpdateLoggingConfiguration(config.LogLevel, config.FileLogging, config.MaxLogFiles);
    }
    
    private void OnConfigurationChanged(object? sender, ConfigurationChangedEventArgs e)
    {
        _logger.LogDebug("Configuration changed, applying logging settings");
        ApplyConfiguration(_config);
    }

    public void Dispose()
    {
        _config.ConfigurationChanged -= OnConfigurationChanged;
    }
}