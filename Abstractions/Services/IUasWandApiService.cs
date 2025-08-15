using Inductobot.Models.Commands;
using Inductobot.Models.Device;
using Inductobot.Models.Measurements;

namespace Inductobot.Abstractions.Services;

/// <summary>
/// High-level API service for UAS-WAND device operations
/// </summary>
public interface IUasWandApiService
{
    /// <summary>
    /// Get device information and status
    /// </summary>
    Task<ApiResponse<UASDeviceInfo>> GetDeviceInfoAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Send keep-alive ping to device
    /// </summary>
    Task<ApiResponse<CodedResponse>> KeepAliveAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get WiFi configuration from device
    /// </summary>
    Task<ApiResponse<WifiConfiguration>> GetWifiSettingsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update WiFi configuration on device
    /// </summary>
    Task<ApiResponse<CodedResponse>> SetWifiSettingsAsync(WifiSettings settings, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Restart device WiFi subsystem
    /// </summary>
    Task<ApiResponse<CodedResponse>> RestartWifiAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Put device into sleep mode
    /// </summary>
    Task<ApiResponse<CodedResponse>> SleepAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Start or stop scanning operations
    /// </summary>
    Task<ApiResponse<ScanStatus>> StartScanAsync(ScanTask task, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get current scan status
    /// </summary>
    Task<ApiResponse<ScanStatus>> GetScanStatusAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get live measurement data
    /// </summary>
    Task<ApiResponse<LiveReadingData>> GetLiveReadingAsync(int startIndex, int numPoints, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get stored measurement data
    /// </summary>
    Task<ApiResponse<MeasurementData>> GetMeasurementAsync(CancellationToken cancellationToken = default);
}