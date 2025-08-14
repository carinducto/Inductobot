using Inductobot.Models.Commands;
using Inductobot.Models.Measurements;
using Inductobot.Models.Device;

namespace Inductobot.Services.Device;

public interface IDeviceCommandService
{
    Task<ApiResponse<UASDeviceInfo>> GetDeviceInfoAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<DeviceStatus>> GetDeviceStatusAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<MeasurementData>> GetMeasurementAsync(MeasurementType type, CancellationToken cancellationToken = default);
    Task<ApiResponse<LiveReadingData>> StartLiveReadingAsync(int sampleRate, int duration, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> StopLiveReadingAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ConfigureDeviceAsync(Dictionary<string, object> settings, CancellationToken cancellationToken = default);
    Task<ApiResponse<Dictionary<string, object>>> GetConfigurationAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ExecuteDiagnosticsAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<T>> SendCustomCommandAsync<T>(string command, Dictionary<string, object> parameters, CancellationToken cancellationToken = default);
}