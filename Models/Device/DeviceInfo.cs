using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Inductobot.Models.Device;

public class UASDeviceInfo : INotifyPropertyChanged
{
    private string _deviceId = string.Empty;
    private string _name = string.Empty;
    private string _ipAddress = string.Empty;
    private int _port = 502; // Default Modbus TCP port
    private string? _firmwareVersion;
    private string? _serialNumber;
    private DeviceType _type;
    private DateTime _lastConnected;
    private DateTime _lastSeen;
    private bool _isOnline;
    private ConnectionState _connectionState = ConnectionState.Disconnected;
    private Dictionary<string, object> _customProperties = new();
    private string? _deviceType;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DeviceId
    {
        get => _deviceId;
        set => SetProperty(ref _deviceId, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string IpAddress
    {
        get => _ipAddress;
        set => SetProperty(ref _ipAddress, value);
    }

    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    public string? FirmwareVersion
    {
        get => _firmwareVersion;
        set => SetProperty(ref _firmwareVersion, value);
    }

    public string? SerialNumber
    {
        get => _serialNumber;
        set => SetProperty(ref _serialNumber, value);
    }

    public DeviceType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    public DateTime LastConnected
    {
        get => _lastConnected;
        set => SetProperty(ref _lastConnected, value);
    }

    public DateTime LastSeen
    {
        get => _lastSeen;
        set => SetProperty(ref _lastSeen, value);
    }

    public bool IsOnline
    {
        get => _isOnline;
        set => SetProperty(ref _isOnline, value);
    }

    public ConnectionState ConnectionState
    {
        get => _connectionState;
        set
        {
            if (SetProperty(ref _connectionState, value))
            {
                // Notify that TrafficLightColor has changed when ConnectionState changes
                OnPropertyChanged(nameof(TrafficLightColor));
            }
        }
    }

    public Dictionary<string, object> CustomProperties
    {
        get => _customProperties;
        set => SetProperty(ref _customProperties, value);
    }

    // Additional property for UI display purposes
    public string? DeviceType
    {
        get => _deviceType;
        set => SetProperty(ref _deviceType, value);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
            return false;

        backingStore = value;
        OnPropertyChanged(propertyName);
        return true;
    }
    
    /// <summary>
    /// Gets the traffic light color based on connection state
    /// </summary>
    public string TrafficLightColor => ConnectionState switch
    {
        ConnectionState.Connected => "Green",
        ConnectionState.Connecting => "Orange", 
        ConnectionState.Reconnecting => "Orange",
        ConnectionState.Disconnecting => "Orange",
        ConnectionState.Error => "Red",
        ConnectionState.Timeout => "Red",
        ConnectionState.Unauthorized => "Red",
        _ => "Gray"
    };
}

public enum DeviceType
{
    WandV3,
    Simulator,
    Custom,
    Generic
}