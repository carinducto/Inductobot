namespace Inductobot.Models.Measurements;

public class MeasurementData
{
    public string MeasurementId { get; set; } = Guid.NewGuid().ToString();
    public string DeviceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public MeasurementType Type { get; set; }
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public double? AverageValue { get; set; }
    public MeasurementStatus Status { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    public MeasurementData()
    {
        Timestamp = DateTime.Now;
        Status = MeasurementStatus.Valid;
    }
}

public enum MeasurementType
{
    Temperature,
    Pressure,
    Thickness,
    Vibration,
    Humidity,
    Flow,
    Level,
    Custom
}

public enum MeasurementStatus
{
    Valid,
    Warning,
    Error,
    OutOfRange,
    Calibrating
}