using Inductobot.Abstractions.Services;
using Inductobot.Models.Commands;
using Inductobot.Models.Device;
using Inductobot.Framework.Device;
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
    private readonly WiFiSettingsManager _wifiManager;
    private readonly ILogger<UasWandControlViewModel> _logger;
    
    private string _statusMessage = "Ready";
    private string _deviceInfoText = "";
    private string _measurementText = "";
    private string _startIndex = "0";
    private string _numPoints = "100";
    private bool _isConnected = false;
    private bool _isBusy = false;
    
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
    
    // WiFi properties now come from the WiFi manager
    public string Ssid
    {
        get => _wifiManager.Ssid;
        set => _wifiManager.Ssid = value;
    }
    
    public string Password
    {
        get => _wifiManager.Password;
        set => _wifiManager.Password = value;
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
    
    public WifiConfiguration? CurrentWifiConfiguration => _wifiManager.CurrentWifiConfiguration;
    
    public bool HasWifiConfiguration => _wifiManager.HasWifiConfiguration;
    
    public UASDeviceInfo? CurrentDevice => _deviceService.CurrentDevice;
    
    public IReadOnlyList<UASDeviceInfo> DiscoveredDevices => _discoveryService.DiscoveredDevices;
    
    public UasWandControlViewModel(
        IUasWandDeviceService deviceService,
        IUasWandApiService apiService,
        IUasWandDiscoveryService discoveryService,
        WiFiSettingsManager wifiManager,
        ILogger<UasWandControlViewModel> logger)
    {
        _deviceService = deviceService;
        _apiService = apiService;
        _discoveryService = discoveryService;
        _wifiManager = wifiManager;
        _logger = logger;
        
        // Subscribe to service events
        _deviceService.ConnectionStateChanged += OnConnectionStateChanged;
        _deviceService.ConnectionProgressChanged += OnConnectionProgressChanged;
        _discoveryService.DeviceDiscovered += OnDeviceDiscovered;
        _discoveryService.DeviceRemoved += OnDeviceRemoved;
        
        // Subscribe to WiFi manager events to update UI
        _wifiManager.PropertyChanged += OnWifiManagerPropertyChanged;
        
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
                
                // Automatically retrieve WiFi settings after successful authentication
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000); // Brief delay to let connection stabilize
                    
                    // Verify device is still connected and authenticated before retrieving WiFi settings
                    if (!IsConnected || _deviceService.CurrentDevice == null)
                    {
                        _logger.LogWarning("Device disconnected or not authenticated - skipping WiFi auto-retrieval");
                        return;
                    }
                    
                    try
                    {
                        // First verify authentication by attempting a device info call
                        _logger.LogDebug("Verifying authentication before WiFi auto-retrieval");
                        var authTest = await _apiService.GetDeviceInfoAsync();
                        
                        if (!authTest.IsSuccess)
                        {
                            if (authTest.ErrorCode == "UNAUTHORIZED")
                            {
                                _logger.LogWarning("Authentication failed - cannot auto-retrieve WiFi settings: {Error}", authTest.Message);
                                StatusMessage = "Connected but authentication required for WiFi settings";
                                return;
                            }
                            else
                            {
                                _logger.LogWarning("Device communication failed - skipping WiFi auto-retrieval: {Error}", authTest.Message);
                                return;
                            }
                        }
                        
                        // Authentication verified - proceed with WiFi retrieval
                        _logger.LogDebug("Authentication verified - proceeding with WiFi auto-retrieval");
                        var wifiResult = await _wifiManager.GetWifiSettingsAsync();
                        if (wifiResult.IsSuccess)
                        {
                            _logger.LogDebug("Auto-retrieved WiFi settings after authenticated connection");
                        }
                        else
                        {
                            _logger.LogWarning("Failed to auto-retrieve WiFi settings: {Message}", wifiResult.Message);
                        }
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
            _logger.LogDebug("🔍 Getting WiFi settings using WiFi manager");
            
            var result = await _wifiManager.GetWifiSettingsAsync();
            
            StatusMessage = result.Message;
            
            if (result.IsSuccess)
            {
                _logger.LogDebug("✅ WiFi settings retrieved successfully");
            }
            else
            {
                _logger.LogWarning("❌ WiFi settings retrieval failed: {Message}", result.Message);
            }
            
            return result.IsSuccess;
        });
    }
    
    public async Task<bool> RefreshWifiSettingsAsync()
    {
        if (IsBusy) return false;
        
        return await ExecuteAsync(async () =>
        {
            StatusMessage = "Refreshing WiFi settings from UAS-WAND device...";
            _logger.LogInformation("🔄 Refreshing WiFi settings using WiFi manager");
            
            var result = await _wifiManager.GetWifiSettingsAsync();
            
            StatusMessage = result.Message;
            
            if (result.IsSuccess)
            {
                _logger.LogInformation("✅ WiFi settings refresh successful");
            }
            else
            {
                _logger.LogWarning("❌ WiFi settings refresh failed: {Message}", result.Message);
            }
            
            return result.IsSuccess;
        });
    }
    
    public async Task<bool> SetWifiSettingsAsync()
    {
        if (IsBusy || !IsConnected || string.IsNullOrWhiteSpace(Ssid)) return false;
        
        return await ExecuteAsync(async () =>
        {
            StatusMessage = "Setting UAS-WAND WiFi settings...";
            _logger.LogInformation("🔧 Setting WiFi settings using WiFi manager");
            
            var result = await _wifiManager.SetWifiSettingsAsync(Ssid, Password, true);
            
            StatusMessage = result.Message;
            
            if (result.IsSuccess)
            {
                _logger.LogInformation("✅ WiFi settings update successful");
            }
            else
            {
                _logger.LogWarning("❌ WiFi settings update failed: {Message}", result.Message);
            }
            
            return result.IsSuccess;
        });
    }
    
    public async Task<bool> RestartWifiAsync()
    {
        if (IsBusy || !IsConnected) return false;
        
        return await ExecuteAsync(async () =>
        {
            StatusMessage = "Restarting UAS-WAND WiFi...";
            _logger.LogInformation("🔄 Restarting WiFi using WiFi manager");
            
            var result = await _wifiManager.RestartWifiAsync();
            
            StatusMessage = result.Message;
            
            if (result.IsSuccess)
            {
                _logger.LogInformation("✅ WiFi restart successful");
            }
            else
            {
                _logger.LogWarning("❌ WiFi restart failed: {Message}", result.Message);
            }
            
            return result.IsSuccess;
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
    
    private void OnWifiManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        try
        {
            Application.Current?.Dispatcher.Dispatch(() =>
            {
                // Forward WiFi manager property changes to UI
                switch (e.PropertyName)
                {
                    case nameof(WiFiSettingsManager.CurrentWifiConfiguration):
                        OnPropertyChanged(nameof(CurrentWifiConfiguration));
                        break;
                    case nameof(WiFiSettingsManager.HasWifiConfiguration):
                        OnPropertyChanged(nameof(HasWifiConfiguration));
                        break;
                    case nameof(WiFiSettingsManager.Ssid):
                        OnPropertyChanged(nameof(Ssid));
                        break;
                    case nameof(WiFiSettingsManager.Password):
                        OnPropertyChanged(nameof(Password));
                        break;
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating UI on WiFi manager property change");
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
            _wifiManager.ClearWifiConfiguration();
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
            _wifiManager.PropertyChanged -= OnWifiManagerPropertyChanged;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during ViewModel disposal");
        }
    }
}