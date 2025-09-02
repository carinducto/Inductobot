namespace Inductobot.Models.Device;

/// <summary>
/// Device configuration that can be set via COM port
/// </summary>
public class DeviceConfiguration
{
    /// <summary>
    /// Device name/identifier
    /// </summary>
    public string? DeviceName { get; set; }
    
    /// <summary>
    /// Sampling rate in Hz
    /// </summary>
    public int SamplingRate { get; set; }
    
    /// <summary>
    /// Measurement mode
    /// </summary>
    public MeasurementMode Mode { get; set; }
    
    /// <summary>
    /// Gain setting
    /// </summary>
    public int Gain { get; set; }
    
    /// <summary>
    /// Filter settings
    /// </summary>
    public FilterSettings? Filter { get; set; }
    
    /// <summary>
    /// Calibration data
    /// </summary>
    public CalibrationData? Calibration { get; set; }
    
    /// <summary>
    /// Power management settings
    /// </summary>
    public PowerSettings? PowerManagement { get; set; }
    
    /// <summary>
    /// Data logging settings
    /// </summary>
    public LoggingSettings? DataLogging { get; set; }
    
    /// <summary>
    /// Network configuration (WiFi, etc.)
    /// </summary>
    public NetworkConfiguration? NetworkConfig { get; set; }
    
    /// <summary>
    /// Trigger settings
    /// </summary>
    public TriggerSettings? Trigger { get; set; }
}

public enum MeasurementMode
{
    Continuous,
    Triggered,
    Burst,
    SingleShot
}

public class FilterSettings
{
    public bool Enabled { get; set; }
    public double LowPassFrequency { get; set; }
    public double HighPassFrequency { get; set; }
    public int Order { get; set; }
}

public class CalibrationData
{
    public double Offset { get; set; }
    public double Scale { get; set; }
    public DateTime LastCalibrationDate { get; set; }
    public string? CalibrationCertificate { get; set; }
}

public class PowerSettings
{
    public bool AutoSleepEnabled { get; set; }
    public int AutoSleepTimeoutSeconds { get; set; }
    public bool LowPowerMode { get; set; }
    public int BatteryWarningPercent { get; set; }
}

public class LoggingSettings
{
    public bool Enabled { get; set; }
    public int MaxFileSizeMB { get; set; }
    public bool CircularBuffer { get; set; }
    public string? LogFormat { get; set; }
}

public class NetworkConfiguration
{
    public string? SSID { get; set; }
    public string? Password { get; set; }
    public bool DHCPEnabled { get; set; }
    public string? StaticIP { get; set; }
    public string? SubnetMask { get; set; }
    public string? Gateway { get; set; }
}

public class TriggerSettings
{
    public TriggerType Type { get; set; }
    public double ThresholdLevel { get; set; }
    public int PreTriggerSamples { get; set; }
    public int PostTriggerSamples { get; set; }
}

public enum TriggerType
{
    Manual,
    Level,
    Edge,
    External
}