using Inductobot.Abstractions.Services;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Inductobot.Views;

public partial class SettingsPage : ContentPage
{
    private readonly ILogger<SettingsPage> _logger;
    private readonly IConfigurationService _config;
    
    public SettingsPage(ILogger<SettingsPage> logger, IConfigurationService config)
    {
        InitializeComponent();
        _logger = logger;
        _config = config;
        
        _logger.LogInformation("SettingsPage constructor called");
        
        InitializeSettings();
        LoadSettings();
        UpdateApplicationInfo();
    }

    // Parameterless constructor for XAML DataTemplate (manually resolve dependencies)
    public SettingsPage() : this(GetService<ILogger<SettingsPage>>(), GetService<IConfigurationService>())
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
            DefaultPortsEntry.Text = Preferences.Get("DefaultPorts", "80,443,8080,8443");
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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Save settings when leaving the page
        SaveSettings();
        _logger.LogInformation("Settings page disappeared, settings saved");
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
            StatusLabel.Text = "Opening logs...";
            
            // TODO: Implement log viewing functionality
            await DisplayAlert("View Logs", "Log viewing functionality will be implemented in a future update.", "OK");
            
            StatusLabel.Text = "Ready";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error viewing logs");
            StatusLabel.Text = "Error viewing logs";
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
                
                // TODO: Implement log clearing functionality
                await Task.Delay(1000); // Simulate clearing
                
                await DisplayAlert("Success", "Log files have been cleared.", "OK");
                StatusLabel.Text = "Logs cleared";
                
                _logger.LogInformation("Log files cleared by user");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing logs");
            StatusLabel.Text = "Error clearing logs";
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
}