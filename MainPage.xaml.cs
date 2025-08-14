using Inductobot.Services.Communication;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Inductobot;

public partial class MainPage : ContentPage
{
    private readonly ByteSnapTcpClient _tcpClient;
    private readonly ILogger<MainPage> _logger;

    public MainPage(ByteSnapTcpClient tcpClient, ILogger<MainPage> logger)
    {
        InitializeComponent();
        _tcpClient = tcpClient;
        _logger = logger;
        
        _tcpClient.ConnectionStateChanged += OnConnectionStateChanged;
        UpdateConnectionUI();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _tcpClient.ConnectionStateChanged -= OnConnectionStateChanged;
    }

    private void OnConnectionStateChanged(object? sender, Models.Device.ConnectionState state)
    {
        MainThread.BeginInvokeOnMainThread(() => UpdateConnectionUI());
    }

    private void UpdateConnectionUI()
    {
        if (_tcpClient.IsConnected)
        {
            ConnectionStatusLabel.Text = "Connected";
            DisconnectButton.IsVisible = true;
            
            if (_tcpClient.CurrentDevice != null)
            {
                DeviceInfoLabel.Text = $"{_tcpClient.CurrentDevice.IpAddress}:{_tcpClient.CurrentDevice.Port}";
            }
        }
        else
        {
            ConnectionStatusLabel.Text = "Not Connected";
            DeviceInfoLabel.Text = "No device selected";
            DisconnectButton.IsVisible = false;
        }
    }

    private async void OnDisconnectClicked(object sender, EventArgs e)
    {
        await _tcpClient.DisconnectAsync();
        StatusLabel.Text = "Disconnected";
    }

    private async void OnGetDeviceInfoClicked(object sender, EventArgs e)
    {
        if (!_tcpClient.IsConnected)
        {
            await DisplayAlert("Error", "Not connected to any device", "OK");
            return;
        }

        try
        {
            StatusLabel.Text = "Getting device info...";
            var response = await _tcpClient.GetDeviceInfoAsync();
            
            if (response.IsSuccess && response.Data != null)
            {
                var info = JsonSerializer.Serialize(response.Data, new JsonSerializerOptions { WriteIndented = true });
                DeviceInfoText.Text = info;
                StatusLabel.Text = "Device info retrieved";
            }
            else
            {
                DeviceInfoText.Text = $"Error: {response.Message}";
                StatusLabel.Text = "Failed to get device info";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting device info");
            await DisplayAlert("Error", ex.Message, "OK");
            StatusLabel.Text = "Error occurred";
        }
    }

    private async void OnKeepAliveClicked(object sender, EventArgs e)
    {
        if (!_tcpClient.IsConnected)
        {
            await DisplayAlert("Error", "Not connected to any device", "OK");
            return;
        }

        try
        {
            StatusLabel.Text = "Sending keep alive...";
            var response = await _tcpClient.KeepAliveAsync();
            
            if (response.IsSuccess)
            {
                StatusLabel.Text = "Keep alive successful";
            }
            else
            {
                StatusLabel.Text = $"Keep alive failed: {response.Message}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending keep alive");
            await DisplayAlert("Error", ex.Message, "OK");
            StatusLabel.Text = "Error occurred";
        }
    }

    private async void OnGetWifiSettingsClicked(object sender, EventArgs e)
    {
        if (!_tcpClient.IsConnected)
        {
            await DisplayAlert("Error", "Not connected to any device", "OK");
            return;
        }

        try
        {
            StatusLabel.Text = "Getting WiFi settings...";
            var response = await _tcpClient.GetWifiSettingsAsync();
            
            if (response.IsSuccess && response.Data != null)
            {
                SsidEntry.Text = response.Data.Ssid ?? "";
                StatusLabel.Text = "WiFi settings retrieved";
            }
            else
            {
                StatusLabel.Text = $"Failed to get WiFi settings: {response.Message}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting WiFi settings");
            await DisplayAlert("Error", ex.Message, "OK");
            StatusLabel.Text = "Error occurred";
        }
    }

    private async void OnSleepDeviceClicked(object sender, EventArgs e)
    {
        if (!_tcpClient.IsConnected)
        {
            await DisplayAlert("Error", "Not connected to any device", "OK");
            return;
        }

        var confirm = await DisplayAlert("Confirm", "Put device to sleep?", "Yes", "No");
        if (!confirm) return;

        try
        {
            StatusLabel.Text = "Putting device to sleep...";
            var response = await _tcpClient.SleepAsync();
            
            if (response.IsSuccess)
            {
                StatusLabel.Text = "Device sleep command sent";
            }
            else
            {
                StatusLabel.Text = $"Sleep failed: {response.Message}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error putting device to sleep");
            await DisplayAlert("Error", ex.Message, "OK");
            StatusLabel.Text = "Error occurred";
        }
    }

    private async void OnStartScanClicked(object sender, EventArgs e)
    {
        if (!_tcpClient.IsConnected)
        {
            await DisplayAlert("Error", "Not connected to any device", "OK");
            return;
        }

        try
        {
            StatusLabel.Text = "Starting scan...";
            var response = await _tcpClient.StartScanAsync(ScanTask.Start);
            
            if (response.IsSuccess)
            {
                StatusLabel.Text = "Scan started";
            }
            else
            {
                StatusLabel.Text = $"Start scan failed: {response.Message}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting scan");
            await DisplayAlert("Error", ex.Message, "OK");
            StatusLabel.Text = "Error occurred";
        }
    }

    private async void OnStopScanClicked(object sender, EventArgs e)
    {
        if (!_tcpClient.IsConnected)
        {
            await DisplayAlert("Error", "Not connected to any device", "OK");
            return;
        }

        try
        {
            StatusLabel.Text = "Stopping scan...";
            var response = await _tcpClient.StartScanAsync(ScanTask.Stop);
            
            if (response.IsSuccess)
            {
                StatusLabel.Text = "Scan stopped";
            }
            else
            {
                StatusLabel.Text = $"Stop scan failed: {response.Message}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping scan");
            await DisplayAlert("Error", ex.Message, "OK");
            StatusLabel.Text = "Error occurred";
        }
    }

    private async void OnGetMeasurementClicked(object sender, EventArgs e)
    {
        if (!_tcpClient.IsConnected)
        {
            await DisplayAlert("Error", "Not connected to any device", "OK");
            return;
        }

        try
        {
            StatusLabel.Text = "Getting measurement...";
            var response = await _tcpClient.GetMeasurementAsync();
            
            if (response.IsSuccess && response.Data != null)
            {
                var measurement = JsonSerializer.Serialize(response.Data, new JsonSerializerOptions { WriteIndented = true });
                MeasurementText.Text = measurement;
                StatusLabel.Text = "Measurement retrieved";
            }
            else
            {
                MeasurementText.Text = $"Error: {response.Message}";
                StatusLabel.Text = "Failed to get measurement";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting measurement");
            await DisplayAlert("Error", ex.Message, "OK");
            StatusLabel.Text = "Error occurred";
        }
    }

    private async void OnGetLiveReadingClicked(object sender, EventArgs e)
    {
        if (!_tcpClient.IsConnected)
        {
            await DisplayAlert("Error", "Not connected to any device", "OK");
            return;
        }

        if (!int.TryParse(StartIndexEntry.Text, out int startIndex) || startIndex < 0)
        {
            await DisplayAlert("Error", "Invalid start index", "OK");
            return;
        }

        if (!int.TryParse(NumPointsEntry.Text, out int numPoints) || numPoints <= 0)
        {
            await DisplayAlert("Error", "Invalid number of points", "OK");
            return;
        }

        try
        {
            StatusLabel.Text = "Getting live reading...";
            var response = await _tcpClient.GetLiveReadingAsync(startIndex, numPoints);
            
            if (response.IsSuccess && response.Data != null)
            {
                var reading = JsonSerializer.Serialize(response.Data, new JsonSerializerOptions { WriteIndented = true });
                MeasurementText.Text = reading;
                StatusLabel.Text = "Live reading retrieved";
            }
            else
            {
                MeasurementText.Text = $"Error: {response.Message}";
                StatusLabel.Text = "Failed to get live reading";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting live reading");
            await DisplayAlert("Error", ex.Message, "OK");
            StatusLabel.Text = "Error occurred";
        }
    }

    private async void OnSetWifiSettingsClicked(object sender, EventArgs e)
    {
        if (!_tcpClient.IsConnected)
        {
            await DisplayAlert("Error", "Not connected to any device", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(SsidEntry.Text))
        {
            await DisplayAlert("Error", "Please enter SSID", "OK");
            return;
        }

        try
        {
            StatusLabel.Text = "Setting WiFi settings...";
            var settings = new WifiSettings
            {
                Ssid = SsidEntry.Text,
                Password = PasswordEntry.Text,
                Enable = true
            };
            
            var response = await _tcpClient.SetWifiSettingsAsync(settings);
            
            if (response.IsSuccess)
            {
                StatusLabel.Text = "WiFi settings updated";
            }
            else
            {
                StatusLabel.Text = $"WiFi settings failed: {response.Message}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting WiFi settings");
            await DisplayAlert("Error", ex.Message, "OK");
            StatusLabel.Text = "Error occurred";
        }
    }

    private async void OnRestartWifiClicked(object sender, EventArgs e)
    {
        if (!_tcpClient.IsConnected)
        {
            await DisplayAlert("Error", "Not connected to any device", "OK");
            return;
        }

        var confirm = await DisplayAlert("Confirm", "Restart WiFi?", "Yes", "No");
        if (!confirm) return;

        try
        {
            StatusLabel.Text = "Restarting WiFi...";
            StatusLabel.Text = "WiFi restart not implemented";
            await DisplayAlert("Info", "WiFi restart functionality needs to be implemented in the device API", "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restarting WiFi");
            await DisplayAlert("Error", ex.Message, "OK");
            StatusLabel.Text = "Error occurred";
        }
    }
}
