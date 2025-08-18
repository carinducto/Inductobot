using Inductobot.ViewModels;
using Inductobot.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace Inductobot;

public partial class MainPage : ContentPage
{
    private readonly UasWandControlViewModel _viewModel;
    private readonly SimulatorControlViewModel _simulatorViewModel;
    private readonly ILogger<MainPage> _logger;

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
        const string originalButtonText = "Get Device Info";
        
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
            // Clear previous content and show immediate visual feedback
            DeviceInfoText.Text = "Loading device information...";
            DeviceInfoText.TextColor = ThemeColors.SecondaryText;
            
            SetButtonLoading(button, "Getting Info...");
            ShowStatusToast("Retrieving device information...", ToastType.Info);
            
            var success = await _viewModel.GetDeviceInfoAsync();
            if (success)
            {
                // Format the device info nicely
                DeviceInfoText.Text = _viewModel.DeviceInfoText;
                DeviceInfoText.TextColor = ThemeColors.SuccessText;
                
                SetButtonSuccess(button, "✅ Got Info");
                ShowStatusToast("Device info retrieved successfully", ToastType.Success);
                
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
                // Show error in the display area
                DeviceInfoText.Text = $"❌ Failed to retrieve device information:\n{_viewModel.StatusMessage}";
                DeviceInfoText.TextColor = ThemeColors.ErrorText;
                
                SetButtonError(button, "❌ Failed");
                ShowStatusToast($"Failed: {_viewModel.StatusMessage}", ToastType.Error);
                
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
            _logger.LogError(ex, "Error getting device info");
            
            // Show error in display area
            DeviceInfoText.Text = $"💥 Unexpected error:\n{ex.Message}";
            DeviceInfoText.TextColor = ThemeColors.ErrorText;
            
            SetButtonError(button, "❌ Error");
            ShowStatusToast($"Error: {ex.Message}", ToastType.Error);
            
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

    private async void OnKeepAliveClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        const string originalButtonText = "Keep Alive";
        
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
            SetButtonLoading(button, "Pinging...");
            ShowStatusToast("Sending keep-alive ping...", ToastType.Info);
            
            var success = await _viewModel.SendKeepAliveAsync();
            if (success)
            {
                SetButtonSuccess(button, "✅ Alive");
                ShowStatusToast("Device responded to keep-alive", ToastType.Success);
                
                // Auto-reset button after success
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1500);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        SetButtonNormal(button, originalButtonText);
                    });
                });
            }
            else
            {
                SetButtonError(button, "❌ No Response");
                ShowStatusToast($"Keep-alive failed: {_viewModel.StatusMessage}", ToastType.Error);
                
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
            _logger.LogError(ex, "Error sending keep alive");
            SetButtonError(button, "❌ Error");
            ShowStatusToast($"Keep-alive error: {ex.Message}", ToastType.Error);
            
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
        const string originalButtonText = "Start Scan";
        
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
            SetButtonLoading(button, "Starting...");
            ShowStatusToast("Starting measurement scan...", ToastType.Info);
            
            var success = await _viewModel.StartScanAsync();
            if (success)
            {
                SetButtonSuccess(button, "✅ Started");
                ShowStatusToast("Measurement scan started successfully", ToastType.Success);
                
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
                ShowStatusToast($"Scan start failed: {_viewModel.StatusMessage}", ToastType.Error);
                
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
            _logger.LogError(ex, "Error starting scan");
            SetButtonError(button, "❌ Error");
            ShowStatusToast($"Scan error: {ex.Message}", ToastType.Error);
            
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

    private async void OnStopScanClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        const string originalButtonText = "Stop Scan";
        
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
            SetButtonLoading(button, "Stopping...");
            ShowStatusToast("Stopping measurement scan...", ToastType.Info);
            
            var success = await _viewModel.StopScanAsync();
            if (success)
            {
                SetButtonSuccess(button, "✅ Stopped");
                ShowStatusToast("Measurement scan stopped successfully", ToastType.Success);
                
                // Auto-reset button after success
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1500);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        SetButtonNormal(button, originalButtonText);
                    });
                });
            }
            else
            {
                SetButtonError(button, "❌ Failed");
                ShowStatusToast($"Stop scan failed: {_viewModel.StatusMessage}", ToastType.Error);
                
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
            _logger.LogError(ex, "Error stopping scan");
            SetButtonError(button, "❌ Error");
            ShowStatusToast($"Stop scan error: {ex.Message}", ToastType.Error);
            
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

    private async void OnGetMeasurementClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        const string originalButtonText = "Get Measurement";
        
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
            // Clear previous content and show immediate visual feedback
            MeasurementText.Text = "Loading measurement data...";
            MeasurementText.TextColor = ThemeColors.SecondaryText;
            
            SetButtonLoading(button, "Reading...");
            ShowStatusToast("Getting measurement data...", ToastType.Info);
            
            var success = await _viewModel.GetMeasurementAsync();
            if (success)
            {
                // Update display with retrieved data
                MeasurementText.Text = _viewModel.MeasurementText;
                MeasurementText.TextColor = ThemeColors.SuccessText;
                
                SetButtonSuccess(button, "✅ Got Data");
                ShowStatusToast("Measurement data retrieved successfully", ToastType.Success);
                
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
                // Show error in the display area
                MeasurementText.Text = $"❌ Failed to retrieve measurement data:\n{_viewModel.StatusMessage}";
                MeasurementText.TextColor = ThemeColors.ErrorText;
                
                SetButtonError(button, "❌ No Data");
                ShowStatusToast($"Measurement failed: {_viewModel.StatusMessage}", ToastType.Error);
                
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
            _logger.LogError(ex, "Error getting measurement");
            
            // Show error in display area
            MeasurementText.Text = $"💥 Unexpected error:\n{ex.Message}";
            MeasurementText.TextColor = ThemeColors.ErrorText;
            
            SetButtonError(button, "❌ Error");
            ShowStatusToast($"Measurement error: {ex.Message}", ToastType.Error);
            
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

    private async void OnGetLiveReadingClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        const string originalButtonText = "Get Live Reading";
        
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
        
        // Input validation
        if (string.IsNullOrWhiteSpace(StartIndexEntry.Text) || !int.TryParse(StartIndexEntry.Text, out int startIndex) || startIndex < 0)
        {
            ShowStatusToast("Please enter a valid start index (0 or greater)", ToastType.Warning);
            StartIndexEntry.Focus();
            return;
        }
        
        if (string.IsNullOrWhiteSpace(NumPointsEntry.Text) || !int.TryParse(NumPointsEntry.Text, out int numPoints) || numPoints <= 0)
        {
            ShowStatusToast("Please enter a valid number of points (1 or greater)", ToastType.Warning);
            NumPointsEntry.Focus();
            return;
        }
        
        try
        {
            // Clear previous content and show immediate visual feedback
            MeasurementText.Text = "Loading live reading data...";
            MeasurementText.TextColor = ThemeColors.SecondaryText;
            
            SetButtonLoading(button, "Reading...");
            ShowStatusToast("Getting live reading data...", ToastType.Info);
            
            // Update ViewModel properties from UI
            _viewModel.StartIndex = StartIndexEntry.Text ?? "0";
            _viewModel.NumPoints = NumPointsEntry.Text ?? "100";
            
            var success = await _viewModel.GetLiveReadingAsync();
            if (success)
            {
                // Update display with retrieved data
                MeasurementText.Text = _viewModel.MeasurementText;
                MeasurementText.TextColor = ThemeColors.SuccessText;
                
                SetButtonSuccess(button, "✅ Got Reading");
                ShowStatusToast("Live reading data retrieved successfully", ToastType.Success);
                
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
                // Show error in the display area
                MeasurementText.Text = $"❌ Failed to retrieve live reading:\n{_viewModel.StatusMessage}";
                MeasurementText.TextColor = ThemeColors.ErrorText;
                
                SetButtonError(button, "❌ Failed");
                ShowStatusToast($"Live reading failed: {_viewModel.StatusMessage}", ToastType.Error);
                
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
            _logger.LogError(ex, "Error getting live reading");
            
            // Show error in display area
            MeasurementText.Text = $"💥 Unexpected error:\n{ex.Message}";
            MeasurementText.TextColor = ThemeColors.ErrorText;
            
            SetButtonError(button, "❌ Error");
            ShowStatusToast($"Live reading error: {ex.Message}", ToastType.Error);
            
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

    private async void OnSetWifiSettingsClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        const string originalButtonText = "Set WiFi Settings";
        
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
        
        // Input validation
        if (string.IsNullOrWhiteSpace(SsidEntry.Text))
        {
            ShowStatusToast("Please enter an SSID", ToastType.Warning);
            SsidEntry.Focus();
            return;
        }
        
        try
        {
            // Immediate visual feedback
            SetButtonLoading(button, "Setting WiFi...");
            ShowStatusToast("Configuring WiFi settings...", ToastType.Info);
            
            // Update ViewModel properties from UI
            _viewModel.Ssid = SsidEntry.Text ?? "";
            _viewModel.Password = PasswordEntry.Text ?? "";
            
            var success = await _viewModel.SetWifiSettingsAsync();
            if (success)
            {
                SetButtonSuccess(button, "✅ WiFi Set");
                ShowStatusToast("WiFi settings configured successfully", ToastType.Success);
                
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
                ShowStatusToast($"WiFi setup failed: {_viewModel.StatusMessage}", ToastType.Error);
                
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
            _logger.LogError(ex, "Error setting WiFi settings");
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
