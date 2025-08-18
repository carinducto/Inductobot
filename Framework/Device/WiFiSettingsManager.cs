using Inductobot.Abstractions.Services;
using Inductobot.Models.Device;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace Inductobot.Framework.Device;

/// <summary>
/// Manages WiFi settings operations with automatic refresh capabilities
/// </summary>
public class WiFiSettingsManager : INotifyPropertyChanged
{
    private readonly IUasWandApiService _apiService;
    private readonly ILogger<WiFiSettingsManager> _logger;
    
    private WifiConfiguration? _currentWifiConfiguration;
    private string _ssid = "";
    private string _password = "";

    public WifiConfiguration? CurrentWifiConfiguration
    {
        get => _currentWifiConfiguration;
        private set
        {
            if (_currentWifiConfiguration != value)
            {
                _currentWifiConfiguration = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasWifiConfiguration));
            }
        }
    }

    public bool HasWifiConfiguration => CurrentWifiConfiguration != null;

    public string Ssid
    {
        get => _ssid;
        set
        {
            if (_ssid != value)
            {
                _ssid = value;
                OnPropertyChanged();
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (_password != value)
            {
                _password = value;
                OnPropertyChanged();
            }
        }
    }

    public WiFiSettingsManager(IUasWandApiService apiService, ILogger<WiFiSettingsManager> logger)
    {
        _apiService = apiService;
        _logger = logger;
    }

    /// <summary>
    /// Get current WiFi settings from the device
    /// </summary>
    public async Task<WiFiOperationResult> GetWifiSettingsAsync()
    {
        try
        {
            _logger.LogDebug("🔍 Getting WiFi settings from device via {ApiServiceType}", _apiService.GetType().Name);
            
            var response = await _apiService.GetWifiSettingsAsync();
            
            _logger.LogDebug("📡 WiFi settings response - Success: {Success}, Data null: {DataNull}, Message: {Message}", 
                response.IsSuccess, response.Data == null, response.Message);
            
            if (response.IsSuccess && response.Data != null)
            {
                _logger.LogDebug("Raw WiFi data: SSID={Ssid}, Password={Password}, Enabled={Enabled}, Channel={Channel}, IP={IP}", 
                    response.Data.Ssid, response.Data.Password, response.Data.Enabled, response.Data.Channel, response.Data.IpAddress);
                
                CurrentWifiConfiguration = response.Data;
                Ssid = response.Data.Ssid ?? "";
                Password = response.Data.Password ?? "";
                
                _logger.LogDebug("WiFi settings updated - SSID={Ssid}, HasConfig={HasConfig}", 
                    Ssid, HasWifiConfiguration);
                
                return WiFiOperationResult.Success("WiFi settings retrieved successfully");
            }
            else if (response.IsSuccess && response.Data == null)
            {
                _logger.LogWarning("WiFi settings response successful but data is null - deserialization issue");
                return WiFiOperationResult.Error("WiFi settings retrieved but data is empty");
            }
            else
            {
                _logger.LogWarning("WiFi settings request failed - Success: {Success}, Message: {Message}, ErrorCode: {ErrorCode}", 
                    response.IsSuccess, response.Message, response.ErrorCode);
                return WiFiOperationResult.Error($"Failed to get WiFi settings: {response.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception getting WiFi settings");
            return WiFiOperationResult.Error($"WiFi settings error: {ex.Message}");
        }
    }

    /// <summary>
    /// Set WiFi settings on the device
    /// </summary>
    public async Task<WiFiOperationResult> SetWifiSettingsAsync(string ssid, string password, bool enabled = true)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ssid))
            {
                return WiFiOperationResult.Error("SSID cannot be empty");
            }

            _logger.LogDebug("🔧 Setting WiFi settings - SSID: {Ssid}, Password: [REDACTED], Enabled: {Enabled}", 
                ssid, enabled);

            var settings = new WifiSettings
            {
                Ssid = ssid,
                Password = password,
                Enable = enabled
            };

            var response = await _apiService.SetWifiSettingsAsync(settings);

            if (response.IsSuccess)
            {
                _logger.LogInformation("WiFi settings updated successfully");
                
                // Brief delay to let settings be processed
                await Task.Delay(1000);
                
                // Refresh to show applied settings
                var refreshResult = await GetWifiSettingsAsync();
                
                if (refreshResult.IsSuccess)
                {
                    return WiFiOperationResult.Success("WiFi settings updated and refreshed successfully");
                }
                else
                {
                    _logger.LogWarning("WiFi settings updated but refresh failed");
                    return WiFiOperationResult.Warning("WiFi settings updated but couldn't refresh current configuration");
                }
            }
            else
            {
                _logger.LogError("WiFi settings update failed: {Message}", response.Message);
                return WiFiOperationResult.Error($"WiFi settings failed: {response.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception setting WiFi settings");
            return WiFiOperationResult.Error($"WiFi settings error: {ex.Message}");
        }
    }

    /// <summary>
    /// Restart WiFi and refresh settings
    /// </summary>
    public async Task<WiFiOperationResult> RestartWifiAsync()
    {
        try
        {
            _logger.LogDebug("🔄 Restarting WiFi");

            var response = await _apiService.RestartWifiAsync();

            if (response.IsSuccess)
            {
                _logger.LogInformation("WiFi restart successful, waiting for stabilization...");
                
                // Wait for WiFi to stabilize after restart
                await Task.Delay(2000);
                
                // Refresh settings to show current configuration
                var refreshResult = await GetWifiSettingsAsync();
                
                if (refreshResult.IsSuccess)
                {
                    return WiFiOperationResult.Success("WiFi restarted and settings refreshed successfully");
                }
                else
                {
                    _logger.LogWarning("WiFi restarted but settings refresh failed");
                    return WiFiOperationResult.Warning("WiFi restarted but couldn't refresh settings - try manual refresh");
                }
            }
            else
            {
                _logger.LogError("WiFi restart failed: {Message}", response.Message);
                return WiFiOperationResult.Error($"WiFi restart failed: {response.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception restarting WiFi");
            return WiFiOperationResult.Error($"WiFi restart error: {ex.Message}");
        }
    }

    /// <summary>
    /// Clear current WiFi configuration (used when disconnecting)
    /// </summary>
    public void ClearWifiConfiguration()
    {
        CurrentWifiConfiguration = null;
        Ssid = "";
        Password = "";
        _logger.LogDebug("WiFi configuration cleared");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Result of a WiFi operation with status and message
/// </summary>
public record WiFiOperationResult(bool IsSuccess, string Message, WiFiOperationResultType Type)
{
    public static WiFiOperationResult Success(string message) => new(true, message, WiFiOperationResultType.Success);
    public static WiFiOperationResult Error(string message) => new(false, message, WiFiOperationResultType.Error);
    public static WiFiOperationResult Warning(string message) => new(true, message, WiFiOperationResultType.Warning);
}

public enum WiFiOperationResultType
{
    Success,
    Warning,
    Error
}