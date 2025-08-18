using Inductobot.Abstractions.Services;
using Inductobot.Models.Debug;
using Inductobot.Services.Debug;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Inductobot.ViewModels;

/// <summary>
/// ViewModel for debug control panel with logging and file management
/// </summary>
public class DebugControlViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DebugConfiguration _config;
    private readonly DebugConsoleService? _consoleService;
    private readonly FileLoggingService? _fileService;
    private readonly IConfigurationService _configService;
    private readonly ILogger<DebugControlViewModel> _logger;
    
    private string _statusText = "";
    private bool _isConsoleVisible = false;
    private bool _isFileLoggingEnabled = false;
    private string _currentLogLevel = "";
    private string _logDirectory = "";
    private string[] _recentLogFiles = Array.Empty<string>();
    
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }
    
    public bool IsConsoleVisible
    {
        get => _isConsoleVisible;
        private set => SetProperty(ref _isConsoleVisible, value);
    }
    
    public bool IsFileLoggingEnabled
    {
        get => _isFileLoggingEnabled;
        private set => SetProperty(ref _isFileLoggingEnabled, value);
    }
    
    public string CurrentLogLevel
    {
        get => _currentLogLevel;
        private set => SetProperty(ref _currentLogLevel, value);
    }
    
    public string LogDirectory
    {
        get => _logDirectory;
        private set => SetProperty(ref _logDirectory, value);
    }
    
    public string[] RecentLogFiles
    {
        get => _recentLogFiles;
        private set => SetProperty(ref _recentLogFiles, value);
    }
    
    // UI Settings from configuration service
    public bool ShowTimestamps => _configService.ShowTimestamps;
    public bool ShowDebugInfo => _configService.ShowDebugInfo;
    
    public DebugControlViewModel(
        DebugConfiguration config,
        DebugConsoleService? consoleService,
        FileLoggingService? fileService,
        IConfigurationService configService,
        ILogger<DebugControlViewModel> logger)
    {
        _config = config;
        _consoleService = consoleService;
        _fileService = fileService;
        _configService = configService;
        _logger = logger;
        
        UpdateStatus();
        
        // Subscribe to configuration changes
        _configService.ConfigurationChanged += OnConfigurationChanged;
        
        if (_fileService != null)
        {
            _ = LoadRecentLogFilesAsync();
        }
    }
    
    private void UpdateStatus()
    {
        IsConsoleVisible = _consoleService?.IsConsoleVisible ?? false;
        IsFileLoggingEnabled = _fileService?.IsFileLoggingEnabled ?? false;
        CurrentLogLevel = IsConsoleVisible ? _config.ConsoleLogLevel.ToString() : "Disabled";
        LogDirectory = IsFileLoggingEnabled ? _config.LogDirectory : "N/A";
        
        var status = new List<string>();
        
        if (IsConsoleVisible)
        {
            status.Add($"Console: {_config.ConsoleLogLevel}");
        }
        
        if (IsFileLoggingEnabled)
        {
            status.Add($"File: {_config.FileLogLevel}");
        }
        
        if (status.Count == 0)
        {
            StatusText = "Debug logging disabled";
        }
        else
        {
            StatusText = $"Debug active - {string.Join(", ", status)}";
        }
    }
    
    public async Task TestLogLevelsAsync()
    {
        try
        {
            _logger.LogTrace("🔍 This is a TRACE level message - most verbose");
            _logger.LogDebug("🐛 This is a DEBUG level message - detailed diagnostic info");
            _logger.LogInformation("ℹ️ This is an INFO level message - general information");
            _logger.LogWarning("⚠️ This is a WARNING level message - something unexpected");
            _logger.LogError("❌ This is an ERROR level message - something failed");
            _logger.LogCritical("💥 This is a CRITICAL level message - system failure");
            
            // Test with exception
            try
            {
                throw new InvalidOperationException("This is a test exception for debugging");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🧪 Testing exception logging");
            }
            
            _logger.LogInformation("✅ Log level test completed - check console and/or log files");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test log levels");
        }
    }
    
    public async Task LoadRecentLogFilesAsync()
    {
        try
        {
            if (_fileService != null)
            {
                var files = await _fileService.GetRecentLogFiles();
                RecentLogFiles = files.Select(Path.GetFileName).ToArray();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load recent log files");
        }
    }
    
    public async Task<string> GetLogFileContentAsync(string fileName)
    {
        try
        {
            if (_fileService != null && !string.IsNullOrEmpty(fileName))
            {
                var fullPath = Path.Combine(_config.LogDirectory, fileName);
                return await _fileService.GetLogFileContent(fullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get log file content for {FileName}", fileName);
        }
        
        return "";
    }
    
    public async Task OpenLogDirectoryAsync()
    {
        try
        {
            if (Directory.Exists(_config.LogDirectory))
            {
                // Open the log directory in Windows Explorer
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _config.LogDirectory,
                    UseShellExecute = true
                });
                
                _logger.LogInformation("Opened log directory: {LogDirectory}", _config.LogDirectory);
            }
            else
            {
                _logger.LogWarning("Log directory does not exist: {LogDirectory}", _config.LogDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open log directory");
        }
    }
    
    public async Task ClearLogFilesAsync()
    {
        try
        {
            if (Directory.Exists(_config.LogDirectory))
            {
                var logFiles = Directory.GetFiles(_config.LogDirectory, "inductobot_*.log");
                
                foreach (var file in logFiles)
                {
                    try
                    {
                        File.Delete(file);
                        _logger.LogDebug("Deleted log file: {FileName}", Path.GetFileName(file));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete log file: {FileName}", Path.GetFileName(file));
                    }
                }
                
                await LoadRecentLogFilesAsync();
                _logger.LogInformation("Cleared {FileCount} log files", logFiles.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear log files");
        }
    }
    
    public async Task GenerateTestDataAsync()
    {
        try
        {
            _logger.LogInformation("🧪 Generating test debug data...");
            
            // Simulate various scenarios
            _logger.LogDebug("Testing connection to UAS-WAND device...");
            await Task.Delay(100);
            
            _logger.LogInformation("Simulating device discovery...");
            for (int i = 1; i <= 3; i++)
            {
                _logger.LogDebug("Found device: UAS-WAND_{DeviceId} at 192.168.1.{IpSuffix}", 
                    $"DEV{i:000}", 100 + i);
                await Task.Delay(50);
            }
            
            _logger.LogWarning("Device UAS-WAND_DEV002 has low battery (15%)");
            
            try
            {
                throw new TimeoutException("Connection to UAS-WAND_DEV003 timed out after 30 seconds");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to device UAS-WAND_DEV003");
            }
            
            _logger.LogInformation("✅ Test data generation completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate test data");
        }
    }
    
    private bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
            return false;
        
        backingStore = value;
        OnPropertyChanged(propertyName);
        return true;
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    private void OnConfigurationChanged(object? sender, ConfigurationChangedEventArgs e)
    {
        // Refresh UI settings when configuration changes
        OnPropertyChanged(nameof(ShowTimestamps));
        OnPropertyChanged(nameof(ShowDebugInfo));
        _logger.LogInformation("Debug UI settings refreshed from configuration");
    }
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    public void Dispose()
    {
        _configService.ConfigurationChanged -= OnConfigurationChanged;
    }
}