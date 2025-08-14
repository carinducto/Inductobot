using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Inductobot.Services.Core;

public class MessagingService : IMessagingService
{
    private readonly ILogger<MessagingService> _logger;
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<object, Delegate>> _subscriptions = new();

    public MessagingService(ILogger<MessagingService> logger)
    {
        _logger = logger;
    }

    public void Subscribe<TMessage>(object subscriber, Action<TMessage> callback) where TMessage : class
    {
        var messageType = typeof(TMessage);
        
        var subscribers = _subscriptions.GetOrAdd(messageType, _ => new ConcurrentDictionary<object, Delegate>());
        subscribers.AddOrUpdate(subscriber, callback, (_, _) => callback);
        
        _logger.LogDebug($"Subscribed {subscriber.GetType().Name} to {messageType.Name}");
    }

    public void Unsubscribe<TMessage>(object subscriber) where TMessage : class
    {
        var messageType = typeof(TMessage);
        
        if (_subscriptions.TryGetValue(messageType, out var subscribers))
        {
            subscribers.TryRemove(subscriber, out _);
            _logger.LogDebug($"Unsubscribed {subscriber.GetType().Name} from {messageType.Name}");
        }
    }

    public void Send<TMessage>(TMessage message) where TMessage : class
    {
        var messageType = typeof(TMessage);
        
        if (_subscriptions.TryGetValue(messageType, out var subscribers))
        {
            foreach (var (subscriber, callback) in subscribers)
            {
                try
                {
                    if (callback is Action<TMessage> typedCallback)
                    {
                        typedCallback.Invoke(message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error delivering message {messageType.Name} to {subscriber.GetType().Name}");
                }
            }
        }
        
        _logger.LogDebug($"Sent message {messageType.Name} to {subscribers?.Count ?? 0} subscribers");
    }

    public void SendAsync<TMessage>(TMessage message) where TMessage : class
    {
        Task.Run(() => Send(message));
    }
}