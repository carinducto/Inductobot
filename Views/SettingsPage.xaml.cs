using Inductobot.Abstractions.Services;
using Inductobot.Services.Simulation;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Inductobot.Views;

public partial class SettingsPage : ContentPage
{
    private readonly ILogger<SettingsPage> _logger;
    private readonly IConfigurationService _config;
    private readonly ILogViewingService _logService;
    private readonly UasWandSimulatorService _simulatorService;
    
    public SettingsPage(ILogger<SettingsPage> logger, IConfigurationService config, ILogViewingService logService, UasWandSimulatorService simulatorService)
    {
        InitializeComponent();
        _logger = logger;
        _config = config;
        _logService = logService;
        _simulatorService = simulatorService;
        
        _logger.LogInformation("SettingsPage constructor called");
        
        InitializeSettings();
        LoadSettings();
        UpdateApplicationInfo();
        InitializeSimulator();
    }

    // Parameterless constructor for XAML DataTemplate (manually resolve dependencies)
    public SettingsPage() : this(GetService<ILogger<SettingsPage>>(), GetService<IConfigurationService>(), GetService<ILogViewingService>(), GetService<UasWandSimulatorService>())
    {
    }

    private static T GetService<T>() where T : class
    {
        try
        {
            var services = Application.Current?.Handler?.MauiContext?.Services;
            if (services == null)
            {
                throw new InvalidOperationException($"MauiContext.Services not available for {typeof(T).Name}");
            }

            if (services.GetService<T>() is T service)
            {
                return service;
            }
            
            throw new InvalidOperationException($"Service {typeof(T).Name} not registered or not available");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to resolve service {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    private void InitializeSettings()
    {
        try
        {
            // Set default picker selections
            ThemePicker.SelectedIndex = 0; // System
            LogLevelPicker.SelectedIndex = 2; // Information
            
            _logger.LogInformation("Settings initialized with default values");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing settings");
        }
    }

    private void LoadSettings()
    {
        try
        {
            // Load settings from preferences
            ConnectionTimeoutEntry.Text = Preferences.Get("ConnectionTimeout", "15");
            RetryAttemptsEntry.Text = Preferences.Get("RetryAttempts", "3");
            KeepAliveIntervalEntry.Text = Preferences.Get("KeepAliveInterval", "30");
            AutoReconnectSwitch.IsToggled = Preferences.Get("AutoReconnect", true);
            
            ScanTimeoutEntry.Text = Preferences.Get("ScanTimeout", "2");
            DefaultPortsEntry.Text = Preferences.Get("DefaultPorts", "80,8080,5000,443,8443");
            AutoDiscoverySwitch.IsToggled = Preferences.Get("AutoDiscovery", false);
            
            var theme = Preferences.Get("Theme", "System");
            ThemePicker.SelectedItem = theme;
            
            ShowTimestampsSwitch.IsToggled = Preferences.Get("ShowTimestamps", true);
            ShowDebugInfoSwitch.IsToggled = Preferences.Get("ShowDebugInfo", false);
            
            var logLevel = Preferences.Get("LogLevel", "Information");
            LogLevelPicker.SelectedItem = logLevel;
            
            FileLoggingSwitch.IsToggled = Preferences.Get("FileLogging", true);
            MaxLogFilesEntry.Text = Preferences.Get("MaxLogFiles", "10");
            
            _logger.LogInformation("Settings loaded successfully");
            StatusLabel.Text = "Settings loaded";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings");
            StatusLabel.Text = "Error loading settings";
        }
    }

    private void SaveSettings()
    {
        try
        {
            // Save connection settings
            Preferences.Set("ConnectionTimeout", ConnectionTimeoutEntry.Text);
            Preferences.Set("RetryAttempts", RetryAttemptsEntry.Text);
            Preferences.Set("KeepAliveInterval", KeepAliveIntervalEntry.Text);
            Preferences.Set("AutoReconnect", AutoReconnectSwitch.IsToggled);
            
            // Save discovery settings
            Preferences.Set("ScanTimeout", ScanTimeoutEntry.Text);
            Preferences.Set("DefaultPorts", DefaultPortsEntry.Text);
            Preferences.Set("AutoDiscovery", AutoDiscoverySwitch.IsToggled);
            
            // Save UI settings
            if (ThemePicker.SelectedItem != null)
            {
                var theme = ThemePicker.SelectedItem.ToString();
                if (!string.IsNullOrEmpty(theme))
                {
                    Preferences.Set("Theme", theme);
                    ApplyThemeChange(theme);
                }
            }
            
            Preferences.Set("ShowTimestamps", ShowTimestampsSwitch.IsToggled);
            Preferences.Set("ShowDebugInfo", ShowDebugInfoSwitch.IsToggled);
            
            // Save logging settings
            if (LogLevelPicker.SelectedItem != null)
            {
                Preferences.Set("LogLevel", LogLevelPicker.SelectedItem.ToString());
            }
            
            Preferences.Set("FileLogging", FileLoggingSwitch.IsToggled);
            Preferences.Set("MaxLogFiles", MaxLogFilesEntry.Text);
            
            _logger.LogInformation("Settings saved successfully");
            StatusLabel.Text = "Settings saved";
            
            // Notify configuration service of changes
            _config.RefreshConfiguration();
            
            // Auto-hide status message
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StatusLabel.Text = "Ready";
                });
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving settings");
            StatusLabel.Text = "Error saving settings";
        }
    }

    private void ApplyThemeChange(string theme)
    {
        try
        {
            var appTheme = theme switch
            {
                "Light" => AppTheme.Light,
                "Dark" => AppTheme.Dark,
                _ => AppTheme.Unspecified
            };

            if (Application.Current != null)
            {
                Application.Current.UserAppTheme = appTheme;
                _logger.LogInformation("Applied theme change: {Theme}", theme);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying theme change");
        }
    }

    private void UpdateApplicationInfo()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            
            VersionLabel.Text = version?.ToString() ?? "Unknown";
            
#if DEBUG
            BuildLabel.Text = "Debug";
#else
            BuildLabel.Text = "Release";
#endif

            PlatformLabel.Text = DeviceInfo.Platform.ToString();
            RuntimeLabel.Text = $".NET {Environment.Version.Major}.{Environment.Version.Minor}";
            
            _logger.LogInformation("Application info updated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating application info");
        }
    }


    private void OnPickerSelectedIndexChanged(object sender, EventArgs e)
    {
        // Save settings immediately when picker values change
        SaveSettings();
    }

    private void OnSwitchToggled(object sender, ToggledEventArgs e)
    {
        // Save settings immediately when switch values change
        SaveSettings();
    }

    private void OnEntryTextChanged(object sender, TextChangedEventArgs e)
    {
        // Debounce saving to avoid too frequent saves while typing
        _ = Task.Run(async () =>
        {
            await Task.Delay(1000); // Wait 1 second after last keystroke
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SaveSettings();
            });
        });
    }

    private async void OnViewLogsClicked(object sender, EventArgs e)
    {
        try
        {
            StatusLabel.Text = "Opening log viewer...";
            
            // Create and navigate to the log viewer page directly
            var logViewerPage = new LogViewerPage();
            await Navigation.PushAsync(logViewerPage);
            
            StatusLabel.Text = "Ready";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening log viewer");
            StatusLabel.Text = "Error opening log viewer";
            await DisplayAlert("Error", $"Failed to open log viewer: {ex.Message}", "OK");
        }
    }

    private async void OnClearLogsClicked(object sender, EventArgs e)
    {
        try
        {
            var confirm = await DisplayAlert("Clear Logs", 
                "Are you sure you want to clear all log files? This action cannot be undone.", 
                "Clear", "Cancel");
            
            if (confirm)
            {
                StatusLabel.Text = "Clearing logs...";
                
                // Get all log files and delete them
                var logFiles = await _logService.GetLogFilesAsync();
                int deletedCount = 0;
                
                foreach (var logFile in logFiles)
                {
                    if (await _logService.DeleteLogFileAsync(logFile.FilePath))
                    {
                        deletedCount++;
                    }
                }
                
                await DisplayAlert("Success", $"Cleared {deletedCount} log files.", "OK");
                StatusLabel.Text = $"Cleared {deletedCount} log files";
                
                _logger.LogInformation("Cleared {DeletedCount} log files by user request", deletedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing logs");
            StatusLabel.Text = "Error clearing logs";
            await DisplayAlert("Error", "Failed to clear log files.", "OK");
        }
    }

    private async void OnExportSettingsClicked(object sender, EventArgs e)
    {
        try
        {
            StatusLabel.Text = "Exporting settings...";
            
            // TODO: Implement settings export functionality
            await DisplayAlert("Export Settings", "Settings export functionality will be implemented in a future update.", "OK");
            
            StatusLabel.Text = "Ready";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting settings");
            StatusLabel.Text = "Error exporting settings";
        }
    }

    private async void OnResetSettingsClicked(object sender, EventArgs e)
    {
        try
        {
            var confirm = await DisplayAlert("Reset Settings", 
                "Are you sure you want to reset all settings to their default values?", 
                "Reset", "Cancel");
            
            if (confirm)
            {
                StatusLabel.Text = "Resetting settings...";
                
                // Clear all preferences
                Preferences.Clear();
                
                // Reload default settings
                InitializeSettings();
                LoadSettings();
                
                await DisplayAlert("Success", "Settings have been reset to defaults.", "OK");
                StatusLabel.Text = "Settings reset";
                
                _logger.LogInformation("Settings reset to defaults by user");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting settings");
            StatusLabel.Text = "Error resetting settings";
        }
    }

    #region UAS-WAND Simulator Control

    private void InitializeSimulator()
    {
        try
        {
            _logger.LogInformation("🔧 Initializing simulator control panel");
            
            // Log current simulator state before subscribing
            _logger.LogInformation("📊 Current simulator state - IsRunning: {IsRunning}", _simulatorService.IsRunning);
            
            // Subscribe to simulator status updates
            _simulatorService.SimulatorStateChanged += OnSimulatorStatusChanged;
            _logger.LogInformation("📡 Subscribed to SimulatorStateChanged event");
            
            // Update initial status
            UpdateSimulatorStatus();
            
            // Set up a periodic status check to catch late-starting simulator
            _ = Task.Run(async () =>
            {
                for (int i = 0; i < 10; i++) // Check for up to 10 seconds
                {
                    await Task.Delay(1000);
                    var currentStatus = _simulatorService.IsRunning;
                    _logger.LogInformation("🕐 Periodic status check #{Check} - IsRunning: {IsRunning}", i + 1, currentStatus);
                    
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        UpdateSimulatorStatus();
                    });
                    
                    if (currentStatus) // If simulator is running, we can stop checking
                    {
                        _logger.LogInformation("🎯 Simulator detected as running, stopping periodic checks");
                        break;
                    }
                }
            });
            
            _logger.LogInformation("✅ Simulator control panel initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error initializing simulator control panel");
        }
    }

    private void UpdateSimulatorStatus()
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var isRunning = _simulatorService.IsRunning;
                var status = _simulatorService.GetStatus();
                
                _logger.LogInformation("🔍 UpdateSimulatorStatus called - IsRunning: {IsRunning}, Status: {@Status}", 
                    isRunning, status);
                
                // Update traffic light status indicator with enhanced colors
                Color statusColor;
                string statusText;
                string statusSubText;
                
                if (isRunning)
                {
                    statusColor = Colors.LimeGreen; // Bright green for running
                    statusText = "Running";
                    statusSubText = "Simulator is active and ready";
                }
                else
                {
                    statusColor = Colors.LightGray; // Gray for stopped
                    statusText = "Stopped";
                    statusSubText = "Simulator is not running";
                }
                
                // Apply traffic light color with smooth visual feedback
                SimulatorStatusIndicator.Fill = new SolidColorBrush(statusColor);
                SimulatorStatusLabel.Text = statusText;
                SimulatorStatusSubLabel.Text = statusSubText;
                
                // Smart button state management
                StartSimulatorButton.IsEnabled = !isRunning; // Enable start only when stopped
                StopSimulatorButton.IsEnabled = isRunning;   // Enable stop only when running
                
                // Visual feedback for button states
                StartSimulatorButton.Opacity = StartSimulatorButton.IsEnabled ? 1.0 : 0.6;
                StopSimulatorButton.Opacity = StopSimulatorButton.IsEnabled ? 1.0 : 0.6;
                
                // Update button text based on running state
                StartSimulatorButton.Text = isRunning ? "Already Running" : "Start Simulator";
                StopSimulatorButton.Text = isRunning ? "Stop Simulator" : "Already Stopped";
                
                // Update detailed info display
                if (isRunning && status != null)
                {
                    var uptime = status.StartTime.HasValue 
                        ? DateTime.Now - status.StartTime.Value 
                        : TimeSpan.Zero;
                    
                    SimulatorInfoLabel.Text = $"🔌 Device: {status.DeviceName}\n" +
                                             $"🌐 Address: {status.IPAddress}:{status.Port}\n" +
                                             $"📱 Firmware: {status.FirmwareVersion}\n" +
                                             $"⏰ Started: {status.StartTime?.ToString("HH:mm:ss") ?? "N/A"}\n" +
                                             $"⏱️ Uptime: {uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
                }
                else
                {
                    SimulatorInfoLabel.Text = "ℹ️ Simulator is not running\n" +
                                             "Click 'Start Simulator' to begin testing";
                }
                
                _logger.LogInformation("✅ Simulator UI updated - Status: {Status}, Buttons: Start={StartEnabled}, Stop={StopEnabled}", 
                    statusText, StartSimulatorButton.IsEnabled, StopSimulatorButton.IsEnabled);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating simulator status display");
            
            // Fallback error state
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SimulatorStatusIndicator.Fill = new SolidColorBrush(Colors.Red);
                SimulatorStatusLabel.Text = "Error";
                SimulatorStatusSubLabel.Text = "Status check failed";
                SimulatorInfoLabel.Text = "⚠️ Unable to determine simulator status";
                
                // Disable both buttons on error
                StartSimulatorButton.IsEnabled = false;
                StopSimulatorButton.IsEnabled = false;
                StartSimulatorButton.Opacity = 0.6;
                StopSimulatorButton.Opacity = 0.6;
            });
        }
    }

    private void OnSimulatorStatusChanged(object? sender, bool isRunning)
    {
        _logger.LogInformation("🔔 SimulatorStateChanged event fired - IsRunning: {IsRunning}", isRunning);
        UpdateSimulatorStatus();
    }

    private async void OnStartSimulatorClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        
        try
        {
            // Show loading state with traffic light
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SimulatorStatusIndicator.Fill = new SolidColorBrush(Colors.Orange);
                SimulatorStatusLabel.Text = "Starting...";
                SimulatorStatusSubLabel.Text = "Please wait while simulator starts";
                
                // Disable both buttons during operation
                StartSimulatorButton.IsEnabled = false;
                StopSimulatorButton.IsEnabled = false;
                StartSimulatorButton.Text = "Starting...";
                StartSimulatorButton.Opacity = 0.6;
                StopSimulatorButton.Opacity = 0.6;
            });
            
            StatusLabel.Text = "Starting UAS-WAND simulator...";
            _logger.LogInformation("User requested simulator start");
            
            var success = await _simulatorService.StartSimulatorAsync();
            
            if (success)
            {
                StatusLabel.Text = "UAS-WAND simulator started successfully";
                _logger.LogInformation("Simulator started successfully by user request");
                
                // Status will be updated by the event handler, but force an immediate update
                UpdateSimulatorStatus();
                
                // Auto-hide status message
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = "Ready");
                });
            }
            else
            {
                StatusLabel.Text = "Failed to start simulator";
                _logger.LogWarning("Failed to start simulator");
                
                // Show error state
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SimulatorStatusIndicator.Fill = new SolidColorBrush(Colors.Red);
                    SimulatorStatusLabel.Text = "Start Failed";
                    SimulatorStatusSubLabel.Text = "Could not start simulator";
                });
                
                await DisplayAlert("Error", "Failed to start the UAS-WAND simulator. Check logs for details.", "OK");
                
                // Reset to stopped state
                UpdateSimulatorStatus();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting simulator");
            StatusLabel.Text = "Error starting simulator";
            
            // Show error state
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SimulatorStatusIndicator.Fill = new SolidColorBrush(Colors.Red);
                SimulatorStatusLabel.Text = "Error";
                SimulatorStatusSubLabel.Text = "Start operation failed";
            });
            
            await DisplayAlert("Error", $"Error starting simulator: {ex.Message}", "OK");
            
            // Reset to stopped state
            UpdateSimulatorStatus();
        }
    }

    private async void OnStopSimulatorClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        
        try
        {
            // Confirmation dialog
            var shouldStop = await DisplayAlert("Stop Simulator", 
                "Are you sure you want to stop the UAS-WAND simulator?", 
                "Stop", "Cancel");
            
            if (!shouldStop)
                return;
            
            // Show stopping state with traffic light
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SimulatorStatusIndicator.Fill = new SolidColorBrush(Colors.Orange);
                SimulatorStatusLabel.Text = "Stopping...";
                SimulatorStatusSubLabel.Text = "Please wait while simulator stops";
                
                // Disable both buttons during operation
                StartSimulatorButton.IsEnabled = false;
                StopSimulatorButton.IsEnabled = false;
                StopSimulatorButton.Text = "Stopping...";
                StartSimulatorButton.Opacity = 0.6;
                StopSimulatorButton.Opacity = 0.6;
            });
            
            StatusLabel.Text = "Stopping UAS-WAND simulator...";
            _logger.LogInformation("User requested simulator stop");
            
            var success = await _simulatorService.StopSimulatorAsync();
            
            if (success)
            {
                StatusLabel.Text = "UAS-WAND simulator stopped";
                _logger.LogInformation("Simulator stopped by user request");
                
                // Status will be updated by the event handler, but force an immediate update
                UpdateSimulatorStatus();
                
                // Auto-hide status message
                _ = Task.Run(async () =>
                {
                    await Task.Delay(2000);
                    MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = "Ready");
                });
            }
            else
            {
                StatusLabel.Text = "Failed to stop simulator";
                _logger.LogWarning("Failed to stop simulator");
                
                // Show error state
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SimulatorStatusIndicator.Fill = new SolidColorBrush(Colors.Red);
                    SimulatorStatusLabel.Text = "Stop Failed";
                    SimulatorStatusSubLabel.Text = "Could not stop simulator";
                });
                
                await DisplayAlert("Error", "Failed to stop the UAS-WAND simulator. Check logs for details.", "OK");
                
                // Reset to current state
                UpdateSimulatorStatus();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping simulator");
            StatusLabel.Text = "Error stopping simulator";
            
            // Show error state
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SimulatorStatusIndicator.Fill = new SolidColorBrush(Colors.Red);
                SimulatorStatusLabel.Text = "Error";
                SimulatorStatusSubLabel.Text = "Stop operation failed";
            });
            
            await DisplayAlert("Error", $"Error stopping simulator: {ex.Message}", "OK");
            
            // Reset to current state
            UpdateSimulatorStatus();
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Refresh settings display when page appears
        UpdateApplicationInfo();
        LoadSettings();
        
        // Force refresh simulator status when page appears - this ensures we show current status
        _logger.LogInformation("🔄 Settings page appeared - refreshing simulator status");
        UpdateSimulatorStatus();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Unsubscribe from simulator events
        if (_simulatorService != null)
        {
            _simulatorService.SimulatorStateChanged -= OnSimulatorStatusChanged;
        }
        
        // Save settings when leaving the page
        SaveSettings();
        _logger.LogInformation("Settings page disappeared, settings saved");
    }

    #endregion
}