namespace Inductobot.Models.Device;

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Error,
    Timeout,
    Unauthorized
}