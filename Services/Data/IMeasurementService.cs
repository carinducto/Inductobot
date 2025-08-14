using Inductobot.Models.Measurements;

namespace Inductobot.Services.Data;

public interface IMeasurementService
{
    event EventHandler<MeasurementData>? MeasurementReceived;
    event EventHandler<SensorReading>? LiveDataReceived;
    
    Task StartCollectingAsync(string deviceId, int intervalMs = 1000, CancellationToken cancellationToken = default);
    Task StopCollectingAsync();
    Task<List<MeasurementData>> GetHistoricalDataAsync(string deviceId, DateTime startTime, DateTime endTime);
    Task<bool> SaveMeasurementAsync(MeasurementData measurement);
    Task<bool> ExportDataAsync(string filePath, List<MeasurementData> measurements, ExportFormat format);
    void ClearCache();
}

public enum ExportFormat
{
    CSV,
    JSON,
    Excel,
    XML
}