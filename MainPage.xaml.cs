using Inductobot.ViewModels;
using Microsoft.Extensions.Logging;

namespace Inductobot;

public partial class MainPage : ContentPage
{
    private readonly UasWandControlViewModel _viewModel;
    private readonly ILogger<MainPage> _logger;

    public MainPage(UasWandControlViewModel viewModel, ILogger<MainPage> logger)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _logger = logger;
        BindingContext = _viewModel;
        
        // Subscribe to ViewModel property changes
        _viewModel.PropertyChanged += OnPropertyChanged;
        
        UpdateConnectionUI();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnPropertyChanged;
            _viewModel.Dispose();
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
        try
        {
            var success = await _viewModel.GetDeviceInfoAsync();
            if (success)
            {
                DeviceInfoText.Text = _viewModel.DeviceInfoText;
            }
            else
            {
                await DisplayAlert("Error", _viewModel.StatusMessage, "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting device info");
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnKeepAliveClicked(object sender, EventArgs e)
    {
        try
        {
            var success = await _viewModel.SendKeepAliveAsync();
            if (!success)
            {
                await DisplayAlert("Error", _viewModel.StatusMessage, "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending keep alive");
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnGetWifiSettingsClicked(object sender, EventArgs e)
    {
        try
        {
            var success = await _viewModel.GetWifiSettingsAsync();
            if (success)
            {
                SsidEntry.Text = _viewModel.Ssid;
            }
            else
            {
                await DisplayAlert("Error", _viewModel.StatusMessage, "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting WiFi settings");
            await DisplayAlert("Error", ex.Message, "OK");
        }
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
        try
        {
            var success = await _viewModel.StartScanAsync();
            if (!success)
            {
                await DisplayAlert("Error", _viewModel.StatusMessage, "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting scan");
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnStopScanClicked(object sender, EventArgs e)
    {
        try
        {
            var success = await _viewModel.StopScanAsync();
            if (!success)
            {
                await DisplayAlert("Error", _viewModel.StatusMessage, "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping scan");
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnGetMeasurementClicked(object sender, EventArgs e)
    {
        try
        {
            var success = await _viewModel.GetMeasurementAsync();
            if (success)
            {
                MeasurementText.Text = _viewModel.MeasurementText;
            }
            else
            {
                await DisplayAlert("Error", _viewModel.StatusMessage, "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting measurement");
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnGetLiveReadingClicked(object sender, EventArgs e)
    {
        try
        {
            // Update ViewModel properties from UI
            _viewModel.StartIndex = StartIndexEntry.Text ?? "0";
            _viewModel.NumPoints = NumPointsEntry.Text ?? "100";
            
            var success = await _viewModel.GetLiveReadingAsync();
            if (success)
            {
                MeasurementText.Text = _viewModel.MeasurementText;
            }
            else
            {
                await DisplayAlert("Error", _viewModel.StatusMessage, "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting live reading");
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnSetWifiSettingsClicked(object sender, EventArgs e)
    {
        try
        {
            // Update ViewModel properties from UI
            _viewModel.Ssid = SsidEntry.Text ?? "";
            _viewModel.Password = PasswordEntry.Text ?? "";
            
            var success = await _viewModel.SetWifiSettingsAsync();
            if (!success)
            {
                await DisplayAlert("Error", _viewModel.StatusMessage, "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting WiFi settings");
            await DisplayAlert("Error", ex.Message, "OK");
        }
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
}
