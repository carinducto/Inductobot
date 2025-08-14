namespace Inductobot.Models.Measurements;

public class SensorReading
{
    public int SensorId { get; set; }
    public string SensorName { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTime Timestamp { get; set; }
    public double? Temperature { get; set; }
    public double? Thickness { get; set; }
    public double? Amplitude { get; set; }
    public double? Frequency { get; set; }
    public double? Phase { get; set; }
    public int? Quality { get; set; } // 0-100
    public bool IsValid { get; set; }
    
    public SensorReading()
    {
        Timestamp = DateTime.Now;
        IsValid = true;
    }
}