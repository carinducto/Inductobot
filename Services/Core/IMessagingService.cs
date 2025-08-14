namespace Inductobot.Services.Core;

public interface IMessagingService
{
    void Subscribe<TMessage>(object subscriber, Action<TMessage> callback) where TMessage : class;
    void Unsubscribe<TMessage>(object subscriber) where TMessage : class;
    void Send<TMessage>(TMessage message) where TMessage : class;
    void SendAsync<TMessage>(TMessage message) where TMessage : class;
}

// Common message types
public record DeviceConnectedMessage(string DeviceId, string DeviceName);
public record DeviceDisconnectedMessage(string DeviceId);
public record MeasurementUpdateMessage(string DeviceId, double Value);
public record ErrorMessage(string Title, string Message, Exception? Exception = null);
public record StatusMessage(string Message, StatusLevel Level = StatusLevel.Info);

public enum StatusLevel
{
    Info,
    Success,
    Warning,
    Error
}