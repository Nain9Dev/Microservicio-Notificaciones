using System.Collections.Concurrent;
using System.Threading.Channels;
using Notificaciones.Domain.Events;

namespace Notificaciones.Api.Telemetry;

/// <summary>
/// Fan-out hub that pushes pipeline lifecycle events to every connected Server-Sent Events client.
///
/// Each subscriber owns a bounded channel configured to drop the oldest entry when full, so a slow
/// or stalled browser can never apply back pressure to the broker consumer nor grow memory.
/// </summary>
public sealed class NotificationStreamBroadcaster
{
    private const int SubscriberQueueCapacity = 128;
    private const int MaxSubscribers = 200;

    private readonly ConcurrentDictionary<Guid, Channel<NotificationLifecycleEvent>> _subscribers = new();

    /// <summary>
    /// Number of consoles currently attached to the live stream.
    /// </summary>
    public int SubscriberCount => _subscribers.Count;

    /// <summary>
    /// Registers a new listener. Dispose the returned subscription to detach it.
    /// </summary>
    /// <returns>The subscription, or null when the connection ceiling has been reached.</returns>
    public Subscription? Subscribe()
    {
        if (_subscribers.Count >= MaxSubscribers)
        {
            return null;
        }

        var channel = Channel.CreateBounded<NotificationLifecycleEvent>(new BoundedChannelOptions(SubscriberQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        var id = Guid.NewGuid();
        _subscribers[id] = channel;

        return new Subscription(id, channel.Reader, () =>
        {
            if (_subscribers.TryRemove(id, out var removed))
            {
                removed.Writer.TryComplete();
            }
        });
    }

    /// <summary>
    /// Delivers an event to every attached listener. Never throws.
    /// </summary>
    public void Broadcast(NotificationLifecycleEvent lifecycleEvent)
    {
        foreach (var subscriber in _subscribers.Values)
        {
            subscriber.Writer.TryWrite(lifecycleEvent);
        }
    }

    /// <summary>
    /// Handle owned by a single SSE connection.
    /// </summary>
    public sealed class Subscription : IDisposable
    {
        private readonly Action _onDispose;
        private bool _disposed;

        internal Subscription(Guid id, ChannelReader<NotificationLifecycleEvent> reader, Action onDispose)
        {
            Id = id;
            Reader = reader;
            _onDispose = onDispose;
        }

        public Guid Id { get; }

        public ChannelReader<NotificationLifecycleEvent> Reader { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _onDispose();
        }
    }
}
