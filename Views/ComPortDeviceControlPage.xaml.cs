using Inductobot.Abstractions.Services;
using Inductobot.Models.Device;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Inductobot.Views;

public partial class ComPortDeviceControlPage : ContentPage
{
    private readonly IUasWandComPortService _comPortService;
    private readonly ILogger<ComPortDeviceControlPage> _logger;
    private readonly StringBuilder _responseLog = new();
    private ComPortInfo? _connectedPort;
    private Timer? _statusUpdateTimer;

    // Constructor for dependency injection
    public ComPortDeviceControlPage(
        IUasWandComPortService comPortService,
        ILogger<ComPortDeviceControlPage> logger)
    {
        InitializeComponent();
        _comPortService = comPortService;
        _logger = logger;
        
        InitializePage();
    }

    // Parameterless constructor for XAML (manually resolve dependencies)
    public ComPortDeviceControlPage() : this(
        GetService<IUasWandComPortService>(),
        GetService<ILogger<ComPortDeviceControlPage>>())
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
        _logger.LogInformation("ComPortDeviceControlPage initialization starting");
        
        SubscribeToEvents();
        StartStatusTimer();
        LoadDeviceInfo();
        InitializeConfigurationDefaults();
        
        _logger.LogInformation("ComPortDeviceControlPage initialization complete");
    }

    private void SubscribeToEvents()
    {
        _comPortService.ConnectionStateChanged += OnConnectionStateChanged;
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

    private void LoadDeviceInfo()
    {
        try
        {
            if (!_comPortService.IsConnected)
            {
                _logger.LogWarning("COM port is not connected, cannot load device info");
                DisplayAlert("Error", "COM port is not connected", "OK");
                Shell.Current.GoToAsync("..");
                return;
            }

            _connectedPort = _comPortService.ConnectedPort;
            if (_connectedPort != null)
            {
                ComPortLabel.Text = _connectedPort.PortName;
                DeviceDescriptionLabel.Text = _connectedPort.Description;
                ConnectionStatusLabel.Text = "Connected";
                ConnectionStatusLabel.TextColor = Colors.Green;
                
                _logger.LogInformation("COM port info loaded: {PortName} - {Description}", 
                    _connectedPort.PortName, _connectedPort.Description);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load COM port information");
            DisplayAlert("Error", "Failed to load COM port information", "OK");
        }
    }

    private void InitializeConfigurationDefaults()
    {
        DeviceNameEntry.Text = "UAS-WAND-001";
        SamplingRateEntry.Text = "1000";
        ModePicker.SelectedIndex = 0; // Continuous
        GainEntry.Text = "1";
    }

    private async void OnDisconnectClicked(object sender, EventArgs e)
    {
        try
        {
            _logger.LogInformation("User requested disconnect from COM port");
            
            var result = await DisplayAlert("Disconnect", 
                "Are you sure you want to disconnect from this COM port?", 
                "Yes", "No");
            
            if (result)
            {
                await _comPortService.DisconnectAsync();
                await Shell.Current.GoToAsync("..");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during COM port disconnect");
            await DisplayAlert("Error", "Failed to disconnect", "OK");
        }
    }

    private async void OnReadConfigClicked(object sender, EventArgs e)
    {
        try
        {
            ReadConfigButton.IsEnabled = false;
            ReadConfigButton.Text = "Reading...";
            
            var config = await _comPortService.ReadConfigurationAsync();
            if (config != null)
            {
                DeviceNameEntry.Text = config.DeviceName ?? "Unknown";
                SamplingRateEntry.Text = config.SamplingRate.ToString();
                ModePicker.SelectedIndex = (int)config.Mode;
                GainEntry.Text = config.Gain.ToString();
                
                var message = $"Configuration loaded successfully:\n" +
                             $"Device: {config.DeviceName}\n" +
                             $"Sampling Rate: {config.SamplingRate} Hz\n" +
                             $"Mode: {config.Mode}\n" +
                             $"Gain: {config.Gain}";
                
                await DisplayAlert("Configuration Read", message, "OK");
                AppendToResponseLog($"[{DateTime.Now:HH:mm:ss}] Configuration Read: {config.DeviceName}");
            }
            else
            {
                await DisplayAlert("Error", "Failed to read device configuration", "OK");
                AppendToResponseLog($"[{DateTime.Now:HH:mm:ss}] Configuration Read: FAILED");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading configuration");
            await DisplayAlert("Error", "Failed to read configuration", "OK");
        }
        finally
        {
            ReadConfigButton.IsEnabled = true;
            ReadConfigButton.Text = "Read Config";
        }
    }

    private async void OnWriteConfigClicked(object sender, EventArgs e)
    {
        try
        {
            WriteConfigButton.IsEnabled = false;
            WriteConfigButton.Text = "Writing...";
            
            var config = new DeviceConfiguration
            {
                DeviceName = DeviceNameEntry.Text,
                SamplingRate = int.TryParse(SamplingRateEntry.Text, out var rate) ? rate : 1000,
                Mode = ModePicker.SelectedIndex >= 0 ? (MeasurementMode)ModePicker.SelectedIndex : MeasurementMode.Continuous,
                Gain = int.TryParse(GainEntry.Text, out var gain) ? gain : 1
            };
            
            var success = await _comPortService.ConfigureDeviceAsync(config);
            if (success)
            {
                await DisplayAlert("Success", "Configuration written successfully", "OK");
                AppendToResponseLog($"[{DateTime.Now:HH:mm:ss}] Configuration Written: {config.DeviceName}");
            }
            else
            {
                await DisplayAlert("Error", "Failed to write device configuration", "OK");
                AppendToResponseLog($"[{DateTime.Now:HH:mm:ss}] Configuration Write: FAILED");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing configuration");
            await DisplayAlert("Error", "Failed to write configuration", "OK");
        }
        finally
        {
            WriteConfigButton.IsEnabled = true;
            WriteConfigButton.Text = "Write Config";
        }
    }

    private async void OnGetDeviceIdClicked(object sender, EventArgs e)
    {
        try
        {
            DeviceIdButton.IsEnabled = false;
            DeviceIdButton.Text = "Getting ID...";
            
            var response = await _comPortService.SendCommandAsync("ID");
            if (!string.IsNullOrEmpty(response))
            {
                await DisplayAlert("Device ID", response, "OK");
                AppendToResponseLog($"[{DateTime.Now:HH:mm:ss}] Device ID: {response}");
            }
            else
            {
                await DisplayAlert("Error", "No response received", "OK");
                AppendToResponseLog($"[{DateTime.Now:HH:mm:ss}] Device ID: NO RESPONSE");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting device ID");
            await DisplayAlert("Error", "Failed to get device ID", "OK");
        }
        finally
        {
            DeviceIdButton.IsEnabled = true;
            DeviceIdButton.Text = "Device ID";
        }
    }

    private async void OnResetDeviceClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await DisplayAlert("Reset Device", 
                "Are you sure you want to reset the device?", 
                "Yes", "No");
            
            if (result)
            {
                ResetDeviceButton.IsEnabled = false;
                ResetDeviceButton.Text = "Resetting...";
                
                var response = await _comPortService.SendCommandAsync("RESET");
                if (!string.IsNullOrEmpty(response))
                {
                    await DisplayAlert("Reset Complete", "Device has been reset", "OK");
                    AppendToResponseLog($"[{DateTime.Now:HH:mm:ss}] Device Reset: {response}");
                }
                else
                {
                    await DisplayAlert("Error", "No response received", "OK");
                    AppendToResponseLog($"[{DateTime.Now:HH:mm:ss}] Device Reset: NO RESPONSE");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting device");
            await DisplayAlert("Error", "Failed to reset device", "OK");
        }
        finally
        {
            ResetDeviceButton.IsEnabled = true;
            ResetDeviceButton.Text = "Reset Device";
        }
    }

    private async void OnFirmwareInfoClicked(object sender, EventArgs e)
    {
        try
        {
            FirmwareInfoButton.IsEnabled = false;
            FirmwareInfoButton.Text = "Getting Info...";
            
            var response = await _comPortService.SendCommandAsync("VERSION");
            if (!string.IsNullOrEmpty(response))
            {
                await DisplayAlert("Firmware Information", response, "OK");
                AppendToResponseLog($"[{DateTime.Now:HH:mm:ss}] Firmware Info: {response}");
            }
            else
            {
                await DisplayAlert("Error", "No response received", "OK");
                AppendToResponseLog($"[{DateTime.Now:HH:mm:ss}] Firmware Info: NO RESPONSE");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting firmware info");
            await DisplayAlert("Error", "Failed to get firmware information", "OK");
        }
        finally
        {
            FirmwareInfoButton.IsEnabled = true;
            FirmwareInfoButton.Text = "Firmware Info";
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
                
                var response = await _comPortService.SendCommandAsync(command);
                if (!string.IsNullOrEmpty(response))
                {
                    await DisplayAlert("Command Response", $"Command: {command}\nResponse: {response}", "OK");
                    AppendToResponseLog($"[{DateTime.Now:HH:mm:ss}] {command}: {response}");
                }
                else
                {
                    await DisplayAlert("No Response", $"Command '{command}' sent but no response received", "OK");
                    AppendToResponseLog($"[{DateTime.Now:HH:mm:ss}] {command}: NO RESPONSE");
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

    private async void OnApplyConfigClicked(object sender, EventArgs e)
    {
        try
        {
            ApplyConfigButton.IsEnabled = false;
            ApplyConfigButton.Text = "Applying...";
            
            // First write the configuration
            var config = new DeviceConfiguration
            {
                DeviceName = DeviceNameEntry.Text,
                SamplingRate = int.TryParse(SamplingRateEntry.Text, out var rate) ? rate : 1000,
                Mode = ModePicker.SelectedIndex >= 0 ? (MeasurementMode)ModePicker.SelectedIndex : MeasurementMode.Continuous,
                Gain = int.TryParse(GainEntry.Text, out var gain) ? gain : 1
            };
            
            var success = await _comPortService.ConfigureDeviceAsync(config);
            if (success)
            {
                await DisplayAlert("Success", "Configuration applied successfully", "OK");
                AppendToResponseLog($"[{DateTime.Now:HH:mm:ss}] Configuration Applied: {config.DeviceName}, Rate: {config.SamplingRate}Hz");
            }
            else
            {
                await DisplayAlert("Error", "Failed to apply configuration", "OK");
                AppendToResponseLog($"[{DateTime.Now:HH:mm:ss}] Configuration Apply: FAILED");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying configuration");
            await DisplayAlert("Error", "Failed to apply configuration", "OK");
        }
        finally
        {
            ApplyConfigButton.IsEnabled = true;
            ApplyConfigButton.Text = "Apply Configuration";
        }
    }

    private void OnClearResponsesClicked(object sender, EventArgs e)
    {
        _responseLog.Clear();
        CommandResponseLabel.Text = "Responses cleared. Use the buttons above to interact with the device.";
        CommandResponseLabel.TextColor = Colors.Gray;
        _logger.LogInformation("Command responses cleared by user");
    }

    private async void OnExportLogClicked(object sender, EventArgs e)
    {
        try
        {
            if (_responseLog.Length == 0)
            {
                await DisplayAlert("No Data", "No command responses available to export", "OK");
                return;
            }

            // For now, just show confirmation
            // In a real implementation, you would save to file or share
            await DisplayAlert("Export Log", "Log export feature coming soon!", "OK");
            _logger.LogInformation("Log export requested (feature pending)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during log export");
            await DisplayAlert("Error", "Failed to export log", "OK");
        }
    }

    private void AppendToResponseLog(string message)
    {
        _responseLog.AppendLine(message);
        
        MainThread.BeginInvokeOnMainThread(() =>
        {
            CommandResponseLabel.Text = _responseLog.ToString();
            CommandResponseLabel.TextColor = Colors.Black;
            
            // Scroll to bottom
            ResponseScrollView.ScrollToAsync(0, CommandResponseLabel.Height, true);
        });
    }

    private void OnConnectionStateChanged(object? sender, bool isConnected)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!isConnected)
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
        _comPortService.ConnectionStateChanged -= OnConnectionStateChanged;
    }
}