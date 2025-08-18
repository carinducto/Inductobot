using Inductobot.ViewModels;
using Inductobot.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace Inductobot;

public partial class MainPage : ContentPage
{
    private readonly UasWandControlViewModel _viewModel;
    private readonly SimulatorControlViewModel _simulatorViewModel;
    private readonly ILogger<MainPage> _logger;
    private bool _isPasswordVisible = false;
    private CancellationTokenSource? _passwordVisibilityTimeoutCts;

    public MainPage(UasWandControlViewModel viewModel, SimulatorControlViewModel simulatorViewModel, ILogger<MainPage> logger)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _simulatorViewModel = simulatorViewModel;
        _logger = logger;
        BindingContext = _viewModel;
        
        // Subscribe to ViewModel property changes
        _viewModel.PropertyChanged += OnPropertyChanged;
        _simulatorViewModel.PropertyChanged += OnSimulatorPropertyChanged;
        
        UpdateConnectionUI();
        UpdateSimulatorUI();
        UpdateScanningStatusUI(ScanningState.Idle);
        UpdateWifiSettingsDisplay();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnPropertyChanged;
            // NOTE: Don't dispose - ViewModel is now Singleton and should persist
        }
        
        if (_simulatorViewModel != null)
        {
            _simulatorViewModel.PropertyChanged -= OnSimulatorPropertyChanged;
        }
        
        // Clean up password visibility timeout and hide password for security
        CancelPasswordVisibilityTimeout();
        _isPasswordVisible = false;
    }

    private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        try
        {
            if (e.PropertyName == nameof(_viewModel.IsConnected) || e.PropertyName == nameof(_viewModel.StatusMessage))
            {
                MainThread.BeginInvokeOnMainThread(() => 
                {
                    try
                    {
                        UpdateConnectionUI();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating connection UI");
                    }
                });
            }
            else if (e.PropertyName == nameof(_viewModel.HasWifiConfiguration) || e.PropertyName == nameof(_viewModel.CurrentWifiConfiguration))
            {
                MainThread.BeginInvokeOnMainThread(() => 
                {
                    try
                    {
                        UpdateWifiSettingsDisplay();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating WiFi settings display");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in PropertyChanged event handler");
        }
    }

    private void UpdateConnectionUI()
    {
        try
        {
            ConnectionStatusLabel.Text = _viewModel.IsConnected ? "Connected" : "Not Connected";
            DisconnectButton.IsVisible = _viewModel.IsConnected;
            StatusLabel.Text = _viewModel.StatusMessage;
            
            // Update traffic light indicator color based on connection state
            if (ConnectionStatusIndicator.Fill is SolidColorBrush brush)
            {
                brush.Color = _viewModel.IsConnected ? Colors.Green : Colors.Red;
            }
            
            if (_viewModel.IsConnected)
            {
                DeviceInfoLabel.Text = "UAS-WAND Device Connected";
            }
            else
            {
                DeviceInfoLabel.Text = "No device selected";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating connection UI elements");
            
            // Set safe fallback values
            try
            {
                ConnectionStatusLabel.Text = "Status Unknown";
                DeviceInfoLabel.Text = "Error";
                DisconnectButton.IsVisible = false;
                
                // Set indicator to gray for error state
                if (ConnectionStatusIndicator.Fill is SolidColorBrush brush)
                {
                    brush.Color = Colors.Gray;
                }
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Critical error: Unable to update UI elements");
            }
        }
    }
    
    private void UpdateWifiSettingsDisplay()
    {
        try
        {
            if (_viewModel.HasWifiConfiguration && _viewModel.CurrentWifiConfiguration != null)
            {
                // Show the settings content and hide the status message
                WifiSettingsStatus.IsVisible = false;
                WifiSettingsContent.IsVisible = true;
                
                var config = _viewModel.CurrentWifiConfiguration;
                
                CurrentSsidLabel.Text = config.Ssid ?? "N/A";
                WifiEnabledLabel.Text = config.Enabled ? "Yes" : "No";
                WifiChannelLabel.Text = config.Channel.ToString();
                WifiIpAddressLabel.Text = config.IpAddress ?? "N/A";
                
                // Update password display based on visibility state
                UpdatePasswordDisplay();
                
                // Auto-populate the entry fields for easy editing (but don't overwrite if user is typing)
                if (string.IsNullOrEmpty(SsidEntry.Text) && !string.IsNullOrEmpty(config.Ssid))
                {
                    SsidEntry.Text = config.Ssid;
                }
            }
            else
            {
                // Show the status message and hide the content
                WifiSettingsStatus.IsVisible = true;
                WifiSettingsContent.IsVisible = false;
                // Reset password visibility when hiding content
                _isPasswordVisible = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating WiFi settings display");
            WifiSettingsStatus.IsVisible = true;
            WifiSettingsContent.IsVisible = false;
        }
    }
    
    private void UpdatePasswordDisplay()
    {
        try
        {
            if (_viewModel.CurrentWifiConfiguration != null)
            {
                var password = _viewModel.CurrentWifiConfiguration.Password;
                
                if (string.IsNullOrEmpty(password))
                {
                    WifiPasswordLabel.Text = "Not set";
                    PasswordVisibilityButton.IsVisible = false;
                    PasswordVisibilityWarning.IsVisible = false;
                }
                else
                {
                    PasswordVisibilityButton.IsVisible = true;
                    
                    if (_isPasswordVisible)
                    {
                        WifiPasswordLabel.Text = password;
                        PasswordVisibilityButton.Text = "🙈"; // Hide icon
                        PasswordVisibilityWarning.IsVisible = true;
                    }
                    else
                    {
                        WifiPasswordLabel.Text = "••••••••";
                        PasswordVisibilityButton.Text = "👁️"; // Show icon  
                        PasswordVisibilityWarning.IsVisible = false;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating password display");
            WifiPasswordLabel.Text = "Error";
            PasswordVisibilityButton.IsVisible = false;
        }
    }
    
    private void OnPasswordVisibilityToggled(object sender, EventArgs e)
    {
        try
        {
            _isPasswordVisible = !_isPasswordVisible;
            UpdatePasswordDisplay();
            
            // Log password visibility toggle for security auditing
            _logger.LogInformation("WiFi password visibility toggled to: {IsVisible}", 
                _isPasswordVisible ? "visible" : "hidden");
            
            // Auto-hide password after 30 seconds for security
            if (_isPasswordVisible)
            {
                StartPasswordVisibilityTimeout();
            }
            else
            {
                CancelPasswordVisibilityTimeout();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling password visibility");
        }
    }
    
    private void StartPasswordVisibilityTimeout()
    {
        try
        {
            // Cancel any existing timeout
            CancelPasswordVisibilityTimeout();
            
            _passwordVisibilityTimeoutCts = new CancellationTokenSource();
            
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(30000, _passwordVisibilityTimeoutCts.Token); // 30 seconds
                    
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _isPasswordVisible = false;
                        UpdatePasswordDisplay();
                        _logger.LogInformation("WiFi password auto-hidden after timeout");
                    });
                }
                catch (OperationCanceledException)
                {
                    // Timeout was cancelled, ignore
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in password visibility timeout");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting password visibility timeout");
        }
    }
    
    private void CancelPasswordVisibilityTimeout()
    {
        try
        {
            _passwordVisibilityTimeoutCts?.Cancel();
            _passwordVisibilityTimeoutCts?.Dispose();
            _passwordVisibilityTimeoutCts = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling password visibility timeout");
        }
    }
    
    private void UpdateScanningStatusUI(ScanningState state, string additionalInfo = "")
    {
        try
        {
            if (ScanningStatusIndicator?.Fill is SolidColorBrush brush && ScanningStatusLabel != null)
            {
                switch (state)
                {
                    case ScanningState.Idle:
                        brush.Color = Colors.Gray;
                        ScanningStatusLabel.Text = "Idle";
                        ScanningStatusLabel.TextColor = Colors.Gray;
                        break;
                        
                    case ScanningState.Starting:
                        brush.Color = Colors.Orange;
                        ScanningStatusLabel.Text = "Starting...";
                        ScanningStatusLabel.TextColor = Colors.Orange;
                        break;
                        
                    case ScanningState.Scanning:
                        brush.Color = Colors.Green;
                        ScanningStatusLabel.Text = "Scanning";
                        ScanningStatusLabel.TextColor = Colors.Green;
                        break;
                        
                    case ScanningState.Stopping:
                        brush.Color = Colors.Orange;
                        ScanningStatusLabel.Text = "Stopping...";
                        ScanningStatusLabel.TextColor = Colors.Orange;
                        break;
                        
                    case ScanningState.Completed:
                        brush.Color = Colors.Blue;
                        ScanningStatusLabel.Text = string.IsNullOrEmpty(additionalInfo) ? "Completed" : $"Done ({additionalInfo})";
                        ScanningStatusLabel.TextColor = Colors.Blue;
                        
                        // Auto-reset to idle after 3 seconds
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(3000);
                            MainThread.BeginInvokeOnMainThread(() => UpdateScanningStatusUI(ScanningState.Idle));
                        });
                        break;
                        
                    case ScanningState.Error:
                        brush.Color = Colors.Red;
                        ScanningStatusLabel.Text = string.IsNullOrEmpty(additionalInfo) ? "Error" : $"Error: {additionalInfo}";
                        ScanningStatusLabel.TextColor = Colors.Red;
                        
                        // Auto-reset to idle after 5 seconds
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(5000);
                            MainThread.BeginInvokeOnMainThread(() => UpdateScanningStatusUI(ScanningState.Idle));
                        });
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating scanning status UI");
        }
    }
    
    private enum ScanningState
    {
        Idle,
        Starting,
        Scanning,
        Stopping,
        Completed,
        Error
    }

    private async void OnDisconnectClicked(object sender, EventArgs e)
    {
        try
        {
            await _viewModel.DisconnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from device");
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnGetDeviceInfoClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        
        var config = new ButtonOperationConfig
        {
            OriginalText = "Device Info",
            LoadingText = "Getting...",
            SuccessText = "✅ Got Info",
            InfoMessage = "Retrieving device information...",
            SuccessMessage = "Device info retrieved successfully"
        };
        
        await ExecuteButtonOperationWithContentAsync(
            button,
            DeviceInfoText,
            () => _viewModel.GetDeviceInfoAsync(),
            config,
            () => _viewModel.DeviceInfoText,
            "Loading device information..."
        );
    }

    private async void OnKeepAliveClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        
        var config = new ButtonOperationConfig
        {
            OriginalText = "Keep Alive",
            LoadingText = "Pinging...",
            SuccessText = "✅ Alive",
            InfoMessage = "Sending keep-alive ping...",
            SuccessMessage = "Device responded to keep-alive",
            SuccessResetDelay = 1500
        };
        
        await ExecuteButtonOperationAsync(button, () => _viewModel.SendKeepAliveAsync(), config);
    }

    private async void OnGetWifiSettingsClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        const string originalButtonText = "Get WiFi Settings";
        
        // Pre-flight validation
        if (!_viewModel.IsConnected)
        {
            ShowStatusToast("Not connected to device. Connect first.", ToastType.Warning);
            return;
        }
        
        if (_viewModel.IsBusy)
        {
            ShowStatusToast("Another operation is in progress. Please wait.", ToastType.Warning);
            return;
        }
        
        try
        {
            // Immediate visual feedback
            SetButtonLoading(button, "Getting WiFi...");
            ShowStatusToast("Retrieving WiFi settings...", ToastType.Info);
            
            var success = await _viewModel.GetWifiSettingsAsync();
            if (success)
            {
                // Update UI with retrieved WiFi settings
                SsidEntry.Text = _viewModel.Ssid;
                
                SetButtonSuccess(button, "✅ Got WiFi");
                ShowStatusToast("WiFi settings retrieved successfully", ToastType.Success);
                
                // Auto-reset button after success
                _ = Task.Run(async () =>
                {
                    await Task.Delay(2000);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        SetButtonNormal(button, originalButtonText);
                    });
                });
            }
            else
            {
                SetButtonError(button, "❌ Failed");
                ShowStatusToast($"Failed to get WiFi: {_viewModel.StatusMessage}", ToastType.Error);
                
                // Auto-reset button after error
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        SetButtonNormal(button, originalButtonText);
                    });
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting WiFi settings");
            SetButtonError(button, "❌ Error");
            ShowStatusToast($"WiFi error: {ex.Message}", ToastType.Error);
            
            // Auto-reset button after error
            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SetButtonNormal(button, originalButtonText);
                });
            });
        }
        // Removed finally block to eliminate timing race conditions
    }

    private async void OnSleepDeviceClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert("Confirm", "Put device to sleep?", "Yes", "No");
        if (!confirm) return;

        try
        {
            // Note: Sleep functionality would need to be added to the ViewModel
            await DisplayAlert("Info", "Sleep functionality not yet implemented in new architecture", "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error putting device to sleep");
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnStartScanClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        
        var config = new ButtonOperationConfig
        {
            OriginalText = "Start Scan",
            LoadingText = "Starting...",
            SuccessText = "✅ Started",
            InfoMessage = "Starting measurement scan...",
            SuccessMessage = "Measurement scan started successfully"
        };
        
        // Update scanning status traffic light
        UpdateScanningStatusUI(ScanningState.Starting);
        
        var success = await ExecuteButtonOperationAsync(button, async () =>
        {
            var result = await _viewModel.StartScanAsync();
            
            // Update traffic light based on result
            if (result)
            {
                UpdateScanningStatusUI(ScanningState.Scanning);
            }
            else
            {
                UpdateScanningStatusUI(ScanningState.Error, _viewModel.StatusMessage);
            }
            
            return result;
        }, config);
        
        // If operation failed at framework level (validation, etc.)
        if (!success && ScanningStatusIndicator?.Fill is SolidColorBrush brush && brush.Color != Colors.Red)
        {
            UpdateScanningStatusUI(ScanningState.Idle);
        }
    }

    private async void OnStopScanClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        
        var config = new ButtonOperationConfig
        {
            OriginalText = "Stop Scan",
            LoadingText = "Stopping...",
            SuccessText = "✅ Stopped",
            InfoMessage = "Stopping measurement scan...",
            SuccessMessage = "Measurement scan stopped successfully",
            SuccessResetDelay = 1500
        };
        
        // Update scanning status traffic light
        UpdateScanningStatusUI(ScanningState.Stopping);
        
        var success = await ExecuteButtonOperationAsync(button, async () =>
        {
            var result = await _viewModel.StopScanAsync();
            
            // Update traffic light based on result
            if (result)
            {
                UpdateScanningStatusUI(ScanningState.Completed);
            }
            else
            {
                UpdateScanningStatusUI(ScanningState.Error, _viewModel.StatusMessage);
            }
            
            return result;
        }, config);
        
        // If operation failed at framework level (validation, etc.)
        if (!success && ScanningStatusIndicator?.Fill is SolidColorBrush brush && brush.Color != Colors.Red)
        {
            UpdateScanningStatusUI(ScanningState.Idle);
        }
    }

    private async void OnGetMeasurementClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        
        var config = new ButtonOperationConfig
        {
            OriginalText = "Measurement",
            LoadingText = "Reading...",
            SuccessText = "✅ Got Data",
            InfoMessage = "Getting measurement data...",
            SuccessMessage = "Measurement data retrieved successfully"
        };
        
        await ExecuteButtonOperationWithContentAsync(
            button,
            MeasurementText,
            () => _viewModel.GetMeasurementAsync(),
            config,
            () => _viewModel.MeasurementText,
            "Loading measurement data..."
        );
    }

    private async void OnGetLiveReadingClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        
        var config = new ButtonOperationConfig
        {
            OriginalText = "Live Reading",
            LoadingText = "Reading...",
            SuccessText = "✅ Got Data",
            InfoMessage = "Getting live reading data...",
            SuccessMessage = "Live reading data retrieved successfully",
            PreValidation = CreateInputValidation(
                (StartIndexEntry, "Start Index", text => int.TryParse(text, out int val) && val >= 0, 
                 "Please enter a valid start index (0 or greater)"),
                (NumPointsEntry, "Number of Points", text => int.TryParse(text, out int val) && val > 0, 
                 "Please enter a valid number of points (1 or greater)")
            )
        };
        
        await ExecuteButtonOperationWithContentAsync(
            button,
            MeasurementText,
            async () =>
            {
                // Update ViewModel properties from UI
                _viewModel.StartIndex = StartIndexEntry.Text ?? "0";
                _viewModel.NumPoints = NumPointsEntry.Text ?? "100";
                return await _viewModel.GetLiveReadingAsync();
            },
            config,
            () => _viewModel.MeasurementText,
            "Loading live reading data..."
        );
    }

    private async void OnSetWifiSettingsClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        
        var config = new ButtonOperationConfig
        {
            OriginalText = "Set WiFi",
            LoadingText = "Setting...",
            SuccessText = "✅ WiFi Set",
            InfoMessage = "Configuring WiFi settings...",
            SuccessMessage = "WiFi settings configured successfully",
            PreValidation = CreateInputValidation(
                (SsidEntry, "SSID", text => !string.IsNullOrWhiteSpace(text), "Please enter an SSID")
            )
        };
        
        await ExecuteButtonOperationAsync(button, async () =>
        {
            // Update ViewModel properties from UI
            _viewModel.Ssid = SsidEntry.Text ?? "";
            _viewModel.Password = PasswordEntry.Text ?? "";
            return await _viewModel.SetWifiSettingsAsync();
        }, config);
    }

    private async void OnRestartWifiClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert("Confirm", "Restart WiFi?", "Yes", "No");
        if (!confirm) return;

        try
        {
            // Note: WiFi restart functionality would need to be added to the ViewModel
            await DisplayAlert("Info", "WiFi restart functionality not yet implemented in new architecture", "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restarting WiFi");
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    #region Simulator Controls

    private void OnSimulatorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        UpdateSimulatorUI();
    }

    private void UpdateSimulatorUI()
    {
        try
        {
            var status = _simulatorViewModel.Status;
            var isRunning = _simulatorViewModel.IsRunning;

            // Update status indicator
            var indicator = SimulatorStatusIndicator.Fill as SolidColorBrush;
            if (indicator != null)
            {
                indicator.Color = isRunning ? Colors.Green : Colors.Gray;
            }

            // Update status text
            SimulatorStatusLabel.Text = _simulatorViewModel.StatusMessage;

            // Update buttons
            StartSimulatorButton.IsEnabled = !isRunning && !_simulatorViewModel.IsLoading;
            StopSimulatorButton.IsEnabled = isRunning && !_simulatorViewModel.IsLoading;
            StartSimulatorButton.Text = _simulatorViewModel.StartButtonText;

            // Update info display
            if (status != null)
            {
                SimulatorInfoLabel.Text = $"Device: {status.DeviceName}\n" +
                                        $"Address: {status.IPAddress}:{status.Port}\n" +
                                        $"Firmware: {status.FirmwareVersion}\n" +
                                        $"Started: {status.StartTime?.ToString("HH:mm:ss") ?? "N/A"}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating simulator UI");
        }
    }

    private async void OnStartSimulatorClicked(object sender, EventArgs e)
    {
        try
        {
            if (_simulatorViewModel.StartCommand.CanExecute(null))
            {
                _simulatorViewModel.StartCommand.Execute(null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting simulator");
            await DisplayAlert("Error", $"Failed to start simulator: {ex.Message}", "OK");
        }
    }

    private async void OnStopSimulatorClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert("Confirm", "Stop the UAS-WAND simulator?", "Yes", "No");
        if (!confirm) return;

        try
        {
            if (_simulatorViewModel.StopCommand.CanExecute(null))
            {
                _simulatorViewModel.StopCommand.Execute(null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping simulator");
            await DisplayAlert("Error", $"Failed to stop simulator: {ex.Message}", "OK");
        }
    }

    #endregion

    #region Theme-Aware Color System
    
    /// <summary>
    /// Gets theme-appropriate colors that work in both Light and Dark modes
    /// </summary>
    private static class ThemeColors
    {
        // Status Colors - Theme aware
        public static Color Success => Application.Current?.RequestedTheme == AppTheme.Dark 
            ? Color.FromArgb("#4CAF50")   // Softer green for dark mode
            : Color.FromArgb("#388E3C");  // Standard green for light mode
            
        public static Color Error => Application.Current?.RequestedTheme == AppTheme.Dark 
            ? Color.FromArgb("#F44336")   // Softer red for dark mode  
            : Color.FromArgb("#D32F2F");  // Standard red for light mode
            
        public static Color Warning => Application.Current?.RequestedTheme == AppTheme.Dark 
            ? Color.FromArgb("#FF9800")   // Softer orange for dark mode
            : Color.FromArgb("#F57C00");  // Standard orange for light mode
            
        public static Color Info => Application.Current?.RequestedTheme == AppTheme.Dark 
            ? Color.FromArgb("#2196F3")   // Softer blue for dark mode
            : Color.FromArgb("#1976D2");  // Standard blue for light mode
            
        // Text Colors - High contrast for readability
        public static Color OnColorText => Colors.White; // Always white text on colored backgrounds
        
        public static Color SecondaryText => Application.Current?.RequestedTheme == AppTheme.Dark 
            ? Color.FromArgb("#B0B0B0")   // Light gray for dark mode
            : Color.FromArgb("#666666");  // Dark gray for light mode
            
        public static Color SuccessText => Application.Current?.RequestedTheme == AppTheme.Dark 
            ? Color.FromArgb("#81C784")   // Light green for dark mode
            : Color.FromArgb("#2E7D32");  // Dark green for light mode
            
        public static Color ErrorText => Application.Current?.RequestedTheme == AppTheme.Dark 
            ? Color.FromArgb("#E57373")   // Light red for dark mode  
            : Color.FromArgb("#C62828");  // Dark red for light mode
    }

    #endregion

    #region Button Operation Framework
    
    /// <summary>
    /// Configuration for button operations
    /// </summary>
    public class ButtonOperationConfig
    {
        public string OriginalText { get; set; } = "";
        public string LoadingText { get; set; } = "Loading...";
        public string SuccessText { get; set; } = "✅ Success";
        public string ErrorTextFormat { get; set; } = "❌ {0}";
        public string InfoMessage { get; set; } = "";
        public string SuccessMessage { get; set; } = "";
        public string ErrorMessageFormat { get; set; } = "Operation failed: {0}";
        public int SuccessResetDelay { get; set; } = 2000;
        public int ErrorResetDelay { get; set; } = 3000;
        public bool RequireConnection { get; set; } = true;
        public bool CheckBusyState { get; set; } = true;
        public Func<Task<bool>>? PreValidation { get; set; }
    }
    
    /// <summary>
    /// Executes a button operation with comprehensive feedback and error handling
    /// </summary>
    private async Task<bool> ExecuteButtonOperationAsync(
        Button button, 
        Func<Task<bool>> operation, 
        ButtonOperationConfig config)
    {
        if (button == null || operation == null) return false;
        
        try
        {
            // Pre-flight validation
            if (config.RequireConnection && !_viewModel.IsConnected)
            {
                ShowStatusToast("Not connected to device. Connect first.", ToastType.Warning);
                return false;
            }
            
            if (config.CheckBusyState && _viewModel.IsBusy)
            {
                ShowStatusToast("Another operation is in progress. Please wait.", ToastType.Warning);
                return false;
            }
            
            // Custom pre-validation
            if (config.PreValidation != null)
            {
                var validationResult = await config.PreValidation();
                if (!validationResult) return false;
            }
            
            // Start operation
            SetButtonLoading(button, config.LoadingText);
            if (!string.IsNullOrEmpty(config.InfoMessage))
            {
                ShowStatusToast(config.InfoMessage, ToastType.Info);
            }
            
            // Execute operation
            var success = await operation();
            
            // Handle result
            if (success)
            {
                SetButtonSuccess(button, config.SuccessText);
                if (!string.IsNullOrEmpty(config.SuccessMessage))
                {
                    ShowStatusToast(config.SuccessMessage, ToastType.Success);
                }
                
                // Auto-reset after success
                _ = ResetButtonAfterDelay(button, config.OriginalText, config.SuccessResetDelay);
                return true;
            }
            else
            {
                var errorText = string.Format(config.ErrorTextFormat, "Failed");
                var errorMessage = string.Format(config.ErrorMessageFormat, _viewModel.StatusMessage);
                
                SetButtonError(button, errorText);
                ShowStatusToast(errorMessage, ToastType.Error);
                
                // Auto-reset after error
                _ = ResetButtonAfterDelay(button, config.OriginalText, config.ErrorResetDelay);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in button operation: {OriginalText}", config.OriginalText);
            
            var errorText = string.Format(config.ErrorTextFormat, "Error");
            var errorMessage = $"Error: {ex.Message}";
            
            SetButtonError(button, errorText);
            ShowStatusToast(errorMessage, ToastType.Error);
            
            // Auto-reset after error
            _ = ResetButtonAfterDelay(button, config.OriginalText, config.ErrorResetDelay);
            return false;
        }
    }
    
    /// <summary>
    /// Executes a button operation with content area updates (for Get Device Info, Get Measurement, etc.)
    /// </summary>
    private async Task<bool> ExecuteButtonOperationWithContentAsync(
        Button button,
        Label contentLabel,
        Func<Task<bool>> operation,
        ButtonOperationConfig config,
        Func<string> getContentOnSuccess,
        string loadingContent = "Loading...")
    {
        if (contentLabel != null)
        {
            contentLabel.Text = loadingContent;
            contentLabel.TextColor = ThemeColors.SecondaryText;
        }
        
        var success = await ExecuteButtonOperationAsync(button, operation, config);
        
        if (success && contentLabel != null)
        {
            contentLabel.Text = getContentOnSuccess();
            contentLabel.TextColor = ThemeColors.SuccessText;
        }
        else if (!success && contentLabel != null)
        {
            contentLabel.Text = $"❌ Failed: {_viewModel.StatusMessage}";
            contentLabel.TextColor = ThemeColors.ErrorText;
        }
        
        return success;
    }
    
    /// <summary>
    /// Resets button after a delay on a background thread
    /// </summary>
    private Task ResetButtonAfterDelay(Button button, string originalText, int delayMs)
    {
        return Task.Run(async () =>
        {
            await Task.Delay(delayMs);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SetButtonNormal(button, originalText);
            });
        });
    }
    
    /// <summary>
    /// Creates input validation function for text entries
    /// </summary>
    private Func<Task<bool>> CreateInputValidation(params (Entry entry, string fieldName, Func<string, bool> validator, string errorMessage)[] validations)
    {
        return () =>
        {
            foreach (var (entry, fieldName, validator, errorMessage) in validations)
            {
                if (entry?.Text == null || !validator(entry.Text))
                {
                    ShowStatusToast(errorMessage, ToastType.Warning);
                    entry?.Focus();
                    return Task.FromResult(false);
                }
            }
            return Task.FromResult(true);
        };
    }

    #endregion

    #region Button State Management

    /// <summary>
    /// Sets a button to loading state with spinner and custom text
    /// </summary>
    private void SetButtonLoading(Button button, string loadingText = null)
    {
        if (button == null) return;
        
        try
        {
            button.IsEnabled = false;
            button.Text = loadingText ?? "Loading...";
            button.BackgroundColor = ThemeColors.Warning;
            button.TextColor = ThemeColors.OnColorText;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error setting button loading state");
        }
    }
    
    /// <summary>
    /// Sets a button to success state with green color and checkmark
    /// Note: Does not auto-reset - caller must handle reset timing
    /// </summary>
    private void SetButtonSuccess(Button button, string successText = null)
    {
        if (button == null) return;
        
        try
        {
            button.Text = successText ?? "✅ Success";
            button.BackgroundColor = ThemeColors.Success;
            button.TextColor = ThemeColors.OnColorText;
            button.IsEnabled = false; // Keep disabled until reset
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error setting button success state");
        }
    }
    
    /// <summary>
    /// Sets a button to error state with red color and X mark
    /// Note: Does not auto-reset - caller must handle reset timing
    /// </summary>
    private void SetButtonError(Button button, string errorText = null)
    {
        if (button == null) return;
        
        try
        {
            button.Text = errorText ?? "❌ Error";
            button.BackgroundColor = ThemeColors.Error;
            button.TextColor = ThemeColors.OnColorText;
            button.IsEnabled = false; // Keep disabled until reset
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error setting button error state");
        }
    }
    
    /// <summary>
    /// Resets a button to normal state
    /// </summary>
    private void SetButtonNormal(Button button, string originalText = null)
    {
        if (button == null) return;
        
        try
        {
            button.IsEnabled = true;
            
            // Clear background and text colors to restore default theme appearance
            button.ClearValue(Button.BackgroundColorProperty);
            button.ClearValue(Button.TextColorProperty);
            
            // Alternative approach - explicitly set to default/transparent
            // button.BackgroundColor = Colors.Transparent;
            // button.TextColor = Colors.Transparent;
            
            // Restore original text based on button name or use provided text
            if (!string.IsNullOrEmpty(originalText))
            {
                button.Text = originalText;
            }
            else
            {
                // Auto-detect original text based on button reference
                RestoreButtonOriginalText(button);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error setting button normal state");
        }
    }
    
    /// <summary>
    /// Restores original button text - should only be called when originalText is not provided
    /// This is a fallback method that should rarely be used
    /// </summary>
    private void RestoreButtonOriginalText(Button button)
    {
        if (button == null) return;
        
        try
        {
            // Since we can't reliably detect the original text, log a warning
            // All button handlers should pass explicit originalText to SetButtonNormal()
            _logger.LogWarning("RestoreButtonOriginalText called without explicit text - this should not happen");
            button.Text = "Button";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error in RestoreButtonOriginalText fallback");
            button.Text = "Button";
        }
    }
    
    /// <summary>
    /// Shows a toast-style message in the status bar with color coding
    /// </summary>
    private async void ShowStatusToast(string message, ToastType type = ToastType.Info, int durationMs = 3000)
    {
        if (StatusLabel == null) return;
        
        try
        {
            var originalText = StatusLabel.Text;
            var originalColor = StatusLabel.TextColor;
            
            // Set status with color coding
            StatusLabel.Text = GetToastIcon(type) + " " + message;
            StatusLabel.TextColor = GetToastColor(type);
            
            // Reset after duration
            await Task.Delay(durationMs);
            
            StatusLabel.Text = originalText;
            StatusLabel.TextColor = originalColor;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error showing status toast");
        }
    }
    
    /// <summary>
    /// Toast notification types
    /// </summary>
    private enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }
    
    /// <summary>
    /// Gets icon for toast type
    /// </summary>
    private string GetToastIcon(ToastType type)
    {
        return type switch
        {
            ToastType.Success => "✅",
            ToastType.Error => "❌",
            ToastType.Warning => "⚠️",
            ToastType.Info => "ℹ️",
            _ => "ℹ️"
        };
    }
    
    /// <summary>
    /// Gets theme-aware color for toast type
    /// </summary>
    private Color GetToastColor(ToastType type)
    {
        return type switch
        {
            ToastType.Success => ThemeColors.Success,
            ToastType.Error => ThemeColors.Error,
            ToastType.Warning => ThemeColors.Warning,
            ToastType.Info => ThemeColors.Info,
            _ => ThemeColors.SecondaryText
        };
    }

    #endregion

}
