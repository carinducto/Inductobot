namespace Inductobot.Models.Measurements;

public class LiveReadingData
{
    public string ReadingId { get; set; } = Guid.NewGuid().ToString();
    public string DeviceId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<SensorReading> Readings { get; set; } = new();
    public int SampleRate { get; set; } // Hz
    public int TotalSamples { get; set; }
    public ReadingStatus Status { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    
    public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;
    
    public LiveReadingData()
    {
        StartTime = DateTime.Now;
        Status = ReadingStatus.InProgress;
    }
}

public enum ReadingStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled
}