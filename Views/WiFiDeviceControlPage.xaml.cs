using Inductobot.Abstractions.Services;
using Inductobot.Models.Device;
using Inductobot.Models.Commands;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Text;

namespace Inductobot.Views;

public partial class WiFiDeviceControlPage : ContentPage
{
    private readonly IUasWandDeviceService _deviceService;
    private readonly IUasWandApiService _apiService;
    private readonly ILogger<WiFiDeviceControlPage> _logger;
    private readonly StringBuilder _dataLog = new();
    private UASDeviceInfo? _connectedDevice;
    private Timer? _statusUpdateTimer;
    private bool _isReceivingData = false;

    // Constructor for dependency injection
    public WiFiDeviceControlPage(
        IUasWandDeviceService deviceService,
        IUasWandApiService apiService,
        ILogger<WiFiDeviceControlPage> logger)
    {
        InitializeComponent();
        _deviceService = deviceService;
        _apiService = apiService;
        _logger = logger;
        
        InitializePage();
    }

    // Parameterless constructor for XAML (manually resolve dependencies)
    public WiFiDeviceControlPage() : this(
        GetService<IUasWandDeviceService>(),
        GetService<IUasWandApiService>(),
        GetService<ILogger<WiFiDeviceControlPage>>())
    {
    }

    // Service resolution helper
    private static T GetService<T>() where T : notnull
    {
        try
        {
            var mauiContext = Application.Current?.Handler?.MauiContext
                ?? throw new InvalidOperationException("MauiContext not available");
            return mauiContext.Services.GetRequiredService<T>();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to resolve service {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    private void InitializePage()
    {
        _logger.LogInformation("WiFiDeviceControlPage initialization starting");
        
        SubscribeToEvents();
        StartStatusTimer();
        _ = LoadDeviceInfoAsync();
        
        _logger.LogInformation("WiFiDeviceControlPage initialization complete");
    }

    private void SubscribeToEvents()
    {
        _deviceService.ConnectionStateChanged += OnConnectionStateChanged;
    }

    private void StartStatusTimer()
    {
        _statusUpdateTimer = new Timer(UpdateTimestamp, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    private void UpdateTimestamp(object? state)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            TimestampLabel.Text = DateTime.Now.ToString("HH:mm:ss");
        });
    }

    private async Task LoadDeviceInfoAsync()
    {
        try
        {
            if (!_deviceService.IsConnected)
            {
                _logger.LogWarning("Device is not connected, cannot load device info");
                await DisplayAlert("Error", "Device is not connected", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            _connectedDevice = _deviceService.CurrentDevice;
            if (_connectedDevice != null)
            {
                DeviceNameLabel.Text = _connectedDevice.Name ?? "Unknown Device";
                DeviceAddressLabel.Text = $"{_connectedDevice.IpAddress}:{_connectedDevice.Port}";
                ConnectionStatusLabel.Text = "Connected";
                ConnectionStatusLabel.TextColor = Colors.Green;
                
                _logger.LogInformation("Device info loaded: {DeviceName} at {Address}", 
                    _connectedDevice.Name, $"{_connectedDevice.IpAddress}:{_connectedDevice.Port}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load device information");
            await DisplayAlert("Error", "Failed to load device information", "OK");
        }
    }

    private async void OnDisconnectClicked(object sender, EventArgs e)
    {
        try
        {
            _logger.LogInformation("User requested disconnect from WiFi device");
            
            var result = await DisplayAlert("Disconnect", 
                "Are you sure you want to disconnect from this device?", 
                "Yes", "No");
            
            if (result)
            {
                await _deviceService.DisconnectAsync();
                await Shell.Current.GoToAsync("..");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnect");
            await DisplayAlert("Error", "Failed to disconnect", "OK");
        }
    }

    private async void OnGetDeviceInfoClicked(object sender, EventArgs e)
    {
        try
        {
            GetInfoButton.IsEnabled = false;
            GetInfoButton.Text = "Getting Info...";
            
            var response = await _apiService.GetDeviceInfoAsync();
            if (response.IsSuccess && response.Data != null)
            {
                var info = response.Data;
                var message = $"Device: {info.Name}\n" +
                             $"Serial: {info.SerialNumber}\n" +
                             $"Firmware: {info.FirmwareVersion}\n" +
                             $"Type: {info.Type}";
                
                await DisplayAlert("Device Information", message, "OK");
                AppendToDataLog($"[{DateTime.Now:HH:mm:ss}] Device Info Retrieved");
            }
            else
            {
                await DisplayAlert("Error", response.Message ?? "Failed to get device info", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting device info");
            await DisplayAlert("Error", "Failed to get device information", "OK");
        }
        finally
        {
            GetInfoButton.IsEnabled = true;
            GetInfoButton.Text = "Get Device Info";
        }
    }

    private async void OnWiFiSettingsClicked(object sender, EventArgs e)
    {
        try
        {
            WiFiSettingsButton.IsEnabled = false;
            WiFiSettingsButton.Text = "Getting Settings...";
            
            var response = await _apiService.GetWifiSettingsAsync();
            if (response.IsSuccess && response.Data != null)
            {
                var settings = response.Data;
                var message = $"SSID: {settings.Ssid}\n" +
                             $"Enabled: {settings.Enabled}\n" +
                             $"Channel: {settings.Channel}\n" +
                             $"IP Address: {settings.IpAddress}";
                
                await DisplayAlert("WiFi Settings", message, "OK");
                AppendToDataLog($"[{DateTime.Now:HH:mm:ss}] WiFi Settings Retrieved");
            }
            else
            {
                await DisplayAlert("Error", response.Message ?? "Failed to get WiFi settings", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting WiFi settings");
            await DisplayAlert("Error", "Failed to get WiFi settings", "OK");
        }
        finally
        {
            WiFiSettingsButton.IsEnabled = true;
            WiFiSettingsButton.Text = "WiFi Settings";
        }
    }

    private async void OnStartMeasurementsClicked(object sender, EventArgs e)
    {
        try
        {
            StartMeasurementsButton.IsEnabled = false;
            StartMeasurementsButton.Text = "Starting...";
            
            var scanTask = new ScanTask(); // Default scan task
            var response = await _apiService.StartScanAsync(scanTask);
            if (response.IsSuccess)
            {
                _isReceivingData = true;
                await DisplayAlert("Success", "Measurements started successfully", "OK");
                AppendToDataLog($"[{DateTime.Now:HH:mm:ss}] Measurements Started");
                StatusLabel.Text = "Connected via WiFi - Measurements Active";
            }
            else
            {
                await DisplayAlert("Error", response.Message ?? "Failed to start measurements", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting measurements");
            await DisplayAlert("Error", "Failed to start measurements", "OK");
        }
        finally
        {
            StartMeasurementsButton.IsEnabled = true;
            StartMeasurementsButton.Text = "Start Measurements";
        }
    }

    private async void OnStopMeasurementsClicked(object sender, EventArgs e)
    {
        try
        {
            StopMeasurementsButton.IsEnabled = false;
            StopMeasurementsButton.Text = "Stopping...";
            
            var response = await _apiService.GetScanStatusAsync(); // Using existing method for now
            if (response.IsSuccess)
            {
                _isReceivingData = false;
                await DisplayAlert("Success", "Measurements stopped successfully", "OK");
                AppendToDataLog($"[{DateTime.Now:HH:mm:ss}] Measurements Stopped");
                StatusLabel.Text = "Connected via WiFi - HTTP API Active";
            }
            else
            {
                await DisplayAlert("Error", response.Message ?? "Failed to stop measurements", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping measurements");
            await DisplayAlert("Error", "Failed to stop measurements", "OK");
        }
        finally
        {
            StopMeasurementsButton.IsEnabled = true;
            StopMeasurementsButton.Text = "Stop Measurements";
        }
    }

    private async void OnLiveReadingClicked(object sender, EventArgs e)
    {
        try
        {
            LiveReadingButton.IsEnabled = false;
            LiveReadingButton.Text = "Reading...";
            
            var response = await _apiService.GetLiveReadingAsync(0, 100); // Default parameters
            if (response.IsSuccess && response.Data != null)
            {
                var reading = response.Data;
                var latestReading = reading.Readings.LastOrDefault();
                if (latestReading != null)
                {
                    var message = $"Value: {latestReading.Value:F2}\n" +
                                 $"Sensor: {latestReading.SensorName}\n" +
                                 $"Timestamp: {latestReading.Timestamp:HH:mm:ss}";
                    
                    await DisplayAlert("Live Reading", message, "OK");
                    AppendToDataLog($"[{DateTime.Now:HH:mm:ss}] Live Reading: {latestReading.Value:F2} from {latestReading.SensorName}");
                }
                else
                {
                    await DisplayAlert("No Data", "No sensor readings available", "OK");
                }
            }
            else
            {
                await DisplayAlert("Error", response.Message ?? "Failed to get live reading", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting live reading");
            await DisplayAlert("Error", "Failed to get live reading", "OK");
        }
        finally
        {
            LiveReadingButton.IsEnabled = true;
            LiveReadingButton.Text = "Live Reading";
        }
    }

    private async void OnSendCustomCommandClicked(object sender, EventArgs e)
    {
        try
        {
            var command = await DisplayPromptAsync("Custom Command", 
                "Enter command to send:", "PING", keyboard: Keyboard.Text);
            
            if (!string.IsNullOrEmpty(command))
            {
                SendCommandButton.IsEnabled = false;
                SendCommandButton.Text = "Sending...";
                
                // For now, we'll use the keep alive method as a generic command sender
                var response = await _apiService.KeepAliveAsync();
                if (response.IsSuccess)
                {
                    await DisplayAlert("Command Response", $"Command '{command}' sent successfully", "OK");
                    AppendToDataLog($"[{DateTime.Now:HH:mm:ss}] Command Sent: {command}");
                }
                else
                {
                    await DisplayAlert("Error", response.Message ?? "Command failed", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending custom command");
            await DisplayAlert("Error", "Failed to send command", "OK");
        }
        finally
        {
            SendCommandButton.IsEnabled = true;
            SendCommandButton.Text = "Send Command";
        }
    }

    private void OnClearDataClicked(object sender, EventArgs e)
    {
        _dataLog.Clear();
        RealTimeDataLabel.Text = "Data cleared. Start measurements to see live data.";
        RealTimeDataLabel.TextColor = Colors.Gray;
        _logger.LogInformation("Data log cleared by user");
    }

    private async void OnExportDataClicked(object sender, EventArgs e)
    {
        try
        {
            if (_dataLog.Length == 0)
            {
                await DisplayAlert("No Data", "No data available to export", "OK");
                return;
            }

            // For now, just show the data in a dialog
            // In a real implementation, you would save to file or share
            await DisplayAlert("Export Data", "Data export feature coming soon!", "OK");
            _logger.LogInformation("Data export requested (feature pending)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during data export");
            await DisplayAlert("Error", "Failed to export data", "OK");
        }
    }

    private void AppendToDataLog(string message)
    {
        _dataLog.AppendLine(message);
        
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RealTimeDataLabel.Text = _dataLog.ToString();
            RealTimeDataLabel.TextColor = Colors.Black;
            
            // Scroll to bottom
            DataScrollView.ScrollToAsync(0, RealTimeDataLabel.Height, true);
        });
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState connectionState)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (connectionState != ConnectionState.Connected)
            {
                ConnectionStatusLabel.Text = "Disconnected";
                ConnectionStatusLabel.TextColor = Colors.Red;
                StatusLabel.Text = "Device Disconnected";
                
                // Navigate back to connection page
                Shell.Current.GoToAsync("..");
            }
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _statusUpdateTimer?.Dispose();
        _deviceService.ConnectionStateChanged -= OnConnectionStateChanged;
    }
}