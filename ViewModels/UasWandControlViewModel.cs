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
                        await GetWifiSettingsInternalAsync();
                        _logger.LogDebug("Auto-retrieved WiFi settings after authenticated connection");
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
            return await GetWifiSettingsInternalAsync();
        });
    }
    
    private async Task<bool> GetWifiSettingsInternalAsync()
    {
        StatusMessage = "Getting UAS-WAND WiFi settings...";
        _logger.LogDebug("🔍 Calling GetWifiSettingsAsync via {ApiServiceType}", _apiService.GetType().Name);
        
        var response = await _apiService.GetWifiSettingsAsync();
        
        _logger.LogDebug("📡 GetWifiSettingsAsync - Response IsSuccess: {IsSuccess}, Data is null: {DataIsNull}, Message: {Message}", 
            response.IsSuccess, response.Data == null, response.Message);
        
        if (response.IsSuccess && response.Data != null)
        {
            _logger.LogDebug("Raw WiFi response data: SSID={Ssid}, Password={Password}, Enabled={Enabled}, Channel={Channel}, IP={IP}", 
                response.Data.Ssid, response.Data.Password, response.Data.Enabled, response.Data.Channel, response.Data.IpAddress);
                
            CurrentWifiConfiguration = response.Data;
            Ssid = response.Data.Ssid ?? "";
            Password = response.Data.Password ?? "";
            StatusMessage = "WiFi settings retrieved successfully";
            
            // Verify the assignment worked
            _logger.LogDebug("After assignment - CurrentWifiConfiguration is null: {IsNull}", CurrentWifiConfiguration == null);
            if (CurrentWifiConfiguration != null)
            {
                _logger.LogDebug("CurrentWifiConfiguration values - SSID: {Ssid}, Enabled: {Enabled}", 
                    CurrentWifiConfiguration.Ssid, CurrentWifiConfiguration.Enabled);
            }
            
            // Explicitly fire property change notifications to ensure UI updates
            OnPropertyChanged(nameof(CurrentWifiConfiguration));
            OnPropertyChanged(nameof(HasWifiConfiguration));
            
            _logger.LogDebug("ViewModel updated - SSID={Ssid}, Password={Password}, Enabled={Enabled}, HasWifiConfiguration={HasConfig}", 
                Ssid, Password, response.Data.Enabled, HasWifiConfiguration);
            return true;
        }
        else if (response.IsSuccess && response.Data == null)
        {
            _logger.LogWarning("WiFi settings response was successful but data is null - this indicates a deserialization issue");
            StatusMessage = "WiFi settings retrieved but data is empty";
            return false;
        }
        else
        {
            _logger.LogWarning("WiFi settings request failed - IsSuccess: {IsSuccess}, Message: {Message}, ErrorCode: {ErrorCode}", 
                response.IsSuccess, response.Message, response.ErrorCode);
            StatusMessage = $"Failed to get WiFi settings: {response.Message}";
            return false;
        }
    }
    
    public async Task<bool> RefreshWifiSettingsAsync()
    {
        _logger.LogInformation("🔄 RefreshWifiSettingsAsync called - IsBusy: {IsBusy}, IsConnected: {IsConnected}, DeviceService.IsConnected: {DeviceServiceConnected}, CurrentDevice: {CurrentDevice}",
            IsBusy, IsConnected, _deviceService.IsConnected, _deviceService.CurrentDevice?.Name ?? "null");
        
        if (IsBusy) 
        {
            _logger.LogWarning("❌ RefreshWifiSettingsAsync blocked - IsBusy: {IsBusy}", IsBusy);
            return false;
        }

        // Force connection state update in case it's out of sync
        UpdateConnectionState();
        _logger.LogInformation("🔄 After UpdateConnectionState - IsConnected: {IsConnected}, DeviceService.IsConnected: {DeviceServiceConnected}", 
            IsConnected, _deviceService.IsConnected);
        
        // Instead of strictly requiring IsConnected, try a fallback approach
        // This handles cases where API service can work but device service connection state is stale
        if (!IsConnected) 
        {
            _logger.LogWarning("⚠️ Device service reports not connected, attempting direct API call as fallback");
            StatusMessage = "Attempting to refresh WiFi settings (connection state may be stale)...";
            
            // Try direct API call as fallback
            return await ExecuteAsync(async () =>
            {
                _logger.LogInformation("🔄 Fallback: Direct API call for WiFi settings refresh");
                var response = await _apiService.GetWifiSettingsAsync();
                
                if (response.IsSuccess && response.Data != null)
                {
                    _logger.LogInformation("✅ Fallback API call successful - updating ViewModel state");
                    
                    CurrentWifiConfiguration = response.Data;
                    Ssid = response.Data.Ssid ?? "";
                    Password = response.Data.Password ?? "";
                    StatusMessage = "WiFi settings refreshed successfully (via fallback)";
                    
                    // Explicitly fire property change notifications
                    OnPropertyChanged(nameof(CurrentWifiConfiguration));
                    OnPropertyChanged(nameof(HasWifiConfiguration));
                    
                    _logger.LogInformation("✅ Fallback WiFi refresh successful - SSID: {Ssid}, HasConfig: {HasConfig}", 
                        CurrentWifiConfiguration?.Ssid, HasWifiConfiguration);
                    return true;
                }
                else
                {
                    StatusMessage = "Failed to refresh WiFi settings - device may be disconnected";
                    _logger.LogWarning("❌ Fallback API call failed - IsSuccess: {IsSuccess}, Message: {Message}", 
                        response.IsSuccess, response.Message);
                    return false;
                }
            });
        }
        
        return await ExecuteAsync(async () =>
        {
            StatusMessage = "Refreshing WiFi settings from UAS-WAND device...";
            _logger.LogInformation("🔄 Starting manual WiFi settings refresh - API Service: {ApiServiceType}", _apiService.GetType().Name);
            
            var success = await GetWifiSettingsInternalAsync();
            
            if (success)
            {
                StatusMessage = "WiFi settings refreshed successfully";
                _logger.LogInformation("✅ Manual WiFi settings refresh completed successfully - SSID: {Ssid}, HasConfig: {HasConfig}", 
                    CurrentWifiConfiguration?.Ssid, HasWifiConfiguration);
            }
            else
            {
                StatusMessage = "Failed to refresh WiFi settings - check device connection";
                _logger.LogWarning("❌ Manual WiFi settings refresh failed - SSID: {Ssid}, HasConfig: {HasConfig}, Connected: {IsConnected}", 
                    CurrentWifiConfiguration?.Ssid, HasWifiConfiguration, IsConnected);
            }
            
            return success;
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
                StatusMessage = "WiFi settings updated - refreshing current configuration...";
                
                // Brief delay to let settings be processed
                await Task.Delay(1000);
                
                // Refresh WiFi settings to show what was actually applied
                var refreshSuccess = await GetWifiSettingsInternalAsync();
                
                if (refreshSuccess)
                {
                    StatusMessage = "WiFi settings updated and current configuration refreshed";
                    _logger.LogInformation("WiFi settings update and refresh completed successfully");
                }
                else
                {
                    StatusMessage = "WiFi settings updated but couldn't refresh current configuration";
                    _logger.LogWarning("WiFi settings update succeeded but refresh failed");
                }
                
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
                StatusMessage = "WiFi restarted successfully - waiting for WiFi to stabilize...";
                
                // Wait longer for WiFi to fully restart and stabilize
                // UAS-WAND devices may need time to apply new settings
                await Task.Delay(2000); // 2 seconds for WiFi to settle
                
                StatusMessage = "Refreshing WiFi settings to show current configuration...";
                
                // Attempt to refresh WiFi settings with retry logic
                var refreshSuccess = await GetWifiSettingsInternalAsync();
                
                if (refreshSuccess)
                {
                    StatusMessage = "WiFi restart completed - settings refreshed";
                    _logger.LogInformation("WiFi restart and settings refresh completed successfully");
                }
                else
                {
                    StatusMessage = "WiFi restarted but couldn't refresh settings - try manual refresh";
                    _logger.LogWarning("WiFi restart succeeded but settings refresh failed");
                }
                
                return true; // Return true as long as restart itself succeeded
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