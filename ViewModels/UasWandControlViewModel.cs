using Inductobot.Abstractions.Services;
using Inductobot.Models.Commands;
using Inductobot.Models.Device;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Inductobot.ViewModels;

/// <summary>
/// Clean UI ViewModel for UAS-WAND device control - only uses high-level services
/// </summary>
public class UasWandControlViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IUasWandDeviceService _deviceService;
    private readonly IUasWandApiService _apiService;
    private readonly IUasWandDiscoveryService _discoveryService;
    private readonly ILogger<UasWandControlViewModel> _logger;
    
    private string _statusMessage = "Ready";
    private string _deviceInfoText = "";
    private string _measurementText = "";
    private string _ssid = "";
    private string _password = "";
    private string _startIndex = "0";
    private string _numPoints = "100";
    private bool _isConnected = false;
    private bool _isBusy = false;
    private WifiConfiguration? _currentWifiConfiguration;
    
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }
    
    public string DeviceInfoText
    {
        get => _deviceInfoText;
        set => SetProperty(ref _deviceInfoText, value);
    }
    
    public string MeasurementText
    {
        get => _measurementText;
        set => SetProperty(ref _measurementText, value);
    }
    
    public string Ssid
    {
        get => _ssid;
        set => SetProperty(ref _ssid, value);
    }
    
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }
    
    public string StartIndex
    {
        get => _startIndex;
        set => SetProperty(ref _startIndex, value);
    }
    
    public string NumPoints
    {
        get => _numPoints;
        set => SetProperty(ref _numPoints, value);
    }
    
    public bool IsConnected
    {
        get => _isConnected;
        private set => SetProperty(ref _isConnected, value);
    }
    
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }
    
    public WifiConfiguration? CurrentWifiConfiguration
    {
        get => _currentWifiConfiguration;
        private set => SetProperty(ref _currentWifiConfiguration, value);
    }
    
    public bool HasWifiConfiguration => CurrentWifiConfiguration != null;
    
    public UASDeviceInfo? CurrentDevice => _deviceService.CurrentDevice;
    
    public IReadOnlyList<UASDeviceInfo> DiscoveredDevices => _discoveryService.DiscoveredDevices;
    
    public UasWandControlViewModel(
        IUasWandDeviceService deviceService,
        IUasWandApiService apiService,
        IUasWandDiscoveryService discoveryService,
        ILogger<UasWandControlViewModel> logger)
    {
        _deviceService = deviceService;
        _apiService = apiService;
        _discoveryService = discoveryService;
        _logger = logger;
        
        // Subscribe to service events
        _deviceService.ConnectionStateChanged += OnConnectionStateChanged;
        _deviceService.ConnectionProgressChanged += OnConnectionProgressChanged;
        _discoveryService.DeviceDiscovered += OnDeviceDiscovered;
        _discoveryService.DeviceRemoved += OnDeviceRemoved;
        
        UpdateConnectionState();
    }
    
    public async Task<bool> ConnectToDeviceAsync(string ipAddress, int port)
    {
        if (IsBusy) return false;
        
        return await ExecuteAsync(async () =>
        {
            StatusMessage = $"Connecting to UAS-WAND at {ipAddress}:{port}...";
            var connected = await _deviceService.ConnectToDeviceAsync(ipAddress, port);
            
            if (connected)
            {
                StatusMessage = $"Connected to UAS-WAND at {ipAddress}:{port}";
                
                // Automatically retrieve WiFi settings after successful connection
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000); // Brief delay to let connection stabilize
                    try
                    {
                        await GetWifiSettingsAsync();
                        _logger.LogDebug("Auto-retrieved WiFi settings after connection");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to auto-retrieve WiFi settings after connection");
                    }
                });
                
                return true;
            }
            else
            {
                StatusMessage = $"Failed to connect to UAS-WAND at {ipAddress}:{port}";
                return false;
            }
        });
    }
    
    public async Task DisconnectAsync()
    {
        if (IsBusy) return;
        
        await ExecuteAsync(async () =>
        {
            StatusMessage = "Disconnecting from UAS-WAND...";
            await _deviceService.DisconnectAsync();
            StatusMessage = "Disconnected from UAS-WAND";
        });
    }
    
    public async Task<bool> GetDeviceInfoAsync()
    {
        if (IsBusy || !IsConnected) return false;
        
        return await ExecuteAsync(async () =>
        {
            StatusMessage = "Getting UAS-WAND device info...";
            var response = await _apiService.GetDeviceInfoAsync();
            
            if (response.IsSuccess && response.Data != null)
            {
                var info = JsonSerializer.Serialize(response.Data, new JsonSerializerOptions { WriteIndented = true });
                DeviceInfoText = info;
                StatusMessage = "Device info retrieved";
                return true;
            }
            else
            {
                DeviceInfoText = $"Error: {response.Message}";
                StatusMessage = "Failed to get device info";
                return false;
            }
        });
    }
    
    public async Task<bool> SendKeepAliveAsync()
    {
        if (IsBusy || !IsConnected) return false;
        
        return await ExecuteAsync(async () =>
        {
            StatusMessage = "Sending keep-alive to UAS-WAND...";
            var response = await _apiService.KeepAliveAsync();
            
            if (response.IsSuccess)
            {
                StatusMessage = "Keep-alive successful";
                return true;
            }
            else
            {
                StatusMessage = $"Keep-alive failed: {response.Message}";
                return false;
            }
        });
    }
    
    public async Task<bool> GetWifiSettingsAsync()
    {
        if (IsBusy || !IsConnected) return false;
        
        return await ExecuteAsync(async () =>
        {
            StatusMessage = "Getting UAS-WAND WiFi settings...";
            var response = await _apiService.GetWifiSettingsAsync();
            
            if (response.IsSuccess && response.Data != null)
            {
                _logger.LogDebug("Raw WiFi response data: SSID={Ssid}, Password={Password}, Enabled={Enabled}, Channel={Channel}, IP={IP}", 
                    response.Data.Ssid, response.Data.Password, response.Data.Enabled, response.Data.Channel, response.Data.IpAddress);
                    
                CurrentWifiConfiguration = response.Data;
                Ssid = response.Data.Ssid ?? "";
                Password = response.Data.Password ?? "";
                StatusMessage = "WiFi settings retrieved";
                
                // Explicitly fire property change notifications to ensure UI updates
                OnPropertyChanged(nameof(CurrentWifiConfiguration));
                OnPropertyChanged(nameof(HasWifiConfiguration));
                
                _logger.LogDebug("ViewModel updated - SSID={Ssid}, Password={Password}, Enabled={Enabled}", 
                    Ssid, Password, response.Data.Enabled);
                return true;
            }
            else
            {
                StatusMessage = $"Failed to get WiFi settings: {response.Message}";
                return false;
            }
        });
    }
    
    public async Task<bool> SetWifiSettingsAsync()
    {
        if (IsBusy || !IsConnected || string.IsNullOrWhiteSpace(Ssid)) return false;
        
        return await ExecuteAsync(async () =>
        {
            StatusMessage = "Setting UAS-WAND WiFi settings...";
            var settings = new WifiSettings
            {
                Ssid = Ssid,
                Password = Password,
                Enable = true
            };
            
            var response = await _apiService.SetWifiSettingsAsync(settings);
            
            if (response.IsSuccess)
            {
                StatusMessage = "WiFi settings updated";
                return true;
            }
            else
            {
                StatusMessage = $"WiFi settings failed: {response.Message}";
                return false;
            }
        });
    }
    
    public async Task<bool> RestartWifiAsync()
    {
        if (IsBusy || !IsConnected) return false;
        
        return await ExecuteAsync(async () =>
        {
            StatusMessage = "Restarting UAS-WAND WiFi...";
            var response = await _apiService.RestartWifiAsync();
            
            if (response.IsSuccess)
            {
                StatusMessage = "WiFi restarted - refreshing settings...";
                // Automatically refresh WiFi settings after restart to show updated configuration
                await Task.Delay(500); // Brief delay to let WiFi settle
                await GetWifiSettingsAsync();
                return true;
            }
            else
            {
                StatusMessage = $"WiFi restart failed: {response.Message}";
                return false;
            }
        });
    }
    
    public async Task<bool> StartScanAsync()
    {
        if (IsBusy || !IsConnected) return false;
        
        return await ExecuteAsync(async () =>
        {
            StatusMessage = "Starting UAS-WAND scan...";
            var response = await _apiService.StartScanAsync(ScanTask.Start);
            
            if (response.IsSuccess)
            {
                StatusMessage = "Scan started";
                return true;
            }
            else
            {
                StatusMessage = $"Start scan failed: {response.Message}";
                return false;
            }
        });
    }
    
    public async Task<bool> StopScanAsync()
    {
        if (IsBusy || !IsConnected) return false;
        
        return await ExecuteAsync(async () =>
        {
            StatusMessage = "Stopping UAS-WAND scan...";
            var response = await _apiService.StartScanAsync(ScanTask.Stop);
            
            if (response.IsSuccess)
            {
                StatusMessage = "Scan stopped";
                return true;
            }
            else
            {
                StatusMessage = $"Stop scan failed: {response.Message}";
                return false;
            }
        });
    }
    
    public async Task<bool> GetMeasurementAsync()
    {
        if (IsBusy || !IsConnected) return false;
        
        return await ExecuteAsync(async () =>
        {
            StatusMessage = "Getting UAS-WAND measurement...";
            var response = await _apiService.GetMeasurementAsync();
            
            if (response.IsSuccess && response.Data != null)
            {
                var measurement = JsonSerializer.Serialize(response.Data, new JsonSerializerOptions { WriteIndented = true });
                MeasurementText = measurement;
                StatusMessage = "Measurement retrieved";
                return true;
            }
            else
            {
                MeasurementText = $"Error: {response.Message}";
                StatusMessage = "Failed to get measurement";
                return false;
            }
        });
    }
    
    public async Task<bool> GetLiveReadingAsync()
    {
        if (IsBusy || !IsConnected) return false;
        
        if (!int.TryParse(StartIndex, out int startIndex) || startIndex < 0)
        {
            StatusMessage = "Invalid start index";
            return false;
        }
        
        if (!int.TryParse(NumPoints, out int numPoints) || numPoints <= 0)
        {
            StatusMessage = "Invalid number of points";
            return false;
        }
        
        return await ExecuteAsync(async () =>
        {
            StatusMessage = "Getting UAS-WAND live reading...";
            var response = await _apiService.GetLiveReadingAsync(startIndex, numPoints);
            
            if (response.IsSuccess && response.Data != null)
            {
                var reading = JsonSerializer.Serialize(response.Data, new JsonSerializerOptions { WriteIndented = true });
                MeasurementText = reading;
                StatusMessage = "Live reading retrieved";
                return true;
            }
            else
            {
                MeasurementText = $"Error: {response.Message}";
                StatusMessage = "Failed to get live reading";
                return false;
            }
        });
    }
    
    public async Task DiscoverDevicesAsync()
    {
        if (IsBusy) return;
        
        await ExecuteAsync(async () =>
        {
            StatusMessage = "Discovering UAS-WAND devices...";
            await _discoveryService.StartScanAsync();
            StatusMessage = $"Found {DiscoveredDevices.Count} UAS-WAND device(s)";
        });
    }
    
    private async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        if (IsBusy)
            return default(T)!;
        
        IsBusy = true;
        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing UAS-WAND operation");
            StatusMessage = $"Error: {ex.Message}";
            return default(T)!;
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    private async Task ExecuteAsync(Func<Task> operation)
    {
        await ExecuteAsync(async () => { await operation(); return true; });
    }
    
    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        try
        {
            Application.Current?.Dispatcher.Dispatch(() =>
            {
                UpdateConnectionState();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating UI on connection state change");
        }
    }
    
    private void OnConnectionProgressChanged(object? sender, string progressMessage)
    {
        try
        {
            Application.Current?.Dispatcher.Dispatch(() =>
            {
                StatusMessage = progressMessage;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating UI on connection progress change");
        }
    }
    
    private void OnDeviceDiscovered(object? sender, UASDeviceInfo device)
    {
        try
        {
            Application.Current?.Dispatcher.Dispatch(() =>
            {
                OnPropertyChanged(nameof(DiscoveredDevices));
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating UI on device discovered");
        }
    }
    
    private void OnDeviceRemoved(object? sender, UASDeviceInfo device)
    {
        try
        {
            Application.Current?.Dispatcher.Dispatch(() =>
            {
                OnPropertyChanged(nameof(DiscoveredDevices));
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating UI on device removed");
        }
    }
    
    private void UpdateConnectionState()
    {
        IsConnected = _deviceService.IsConnected;
        
        if (IsConnected && _deviceService.CurrentDevice != null)
        {
            StatusMessage = $"Connected to {_deviceService.CurrentDevice.Name}";
        }
        else if (!IsConnected)
        {
            StatusMessage = "Not connected to UAS-WAND device";
            // Clear WiFi configuration when disconnected
            CurrentWifiConfiguration = null;
            OnPropertyChanged(nameof(HasWifiConfiguration));
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
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    public void Dispose()
    {
        try
        {
            _deviceService.ConnectionStateChanged -= OnConnectionStateChanged;
            _deviceService.ConnectionProgressChanged -= OnConnectionProgressChanged;
            _discoveryService.DeviceDiscovered -= OnDeviceDiscovered;
            _discoveryService.DeviceRemoved -= OnDeviceRemoved;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during ViewModel disposal");
        }
    }
}