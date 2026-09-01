namespace Notificaciones.Domain.Events;

/// <summary>
/// Ordered stages a notification traverses from HTTP ingestion to final delivery.
/// Each stage is emitted as an observable lifecycle event so downstream consumers
/// (dashboards, tracing, alerting) can reconstruct the full journey of a message.
/// </summary>
public enum NotificationStage
{
    /// <summary>
    /// The HTTP gateway validated the payload and created the domain event.
    /// </summary>
    Accepted = 1,

    /// <summary>
    /// The event was handed over to the message broker exchange.
    /// </summary>
    Queued = 2,

    /// <summary>
    /// A worker instance dequeued the event and started processing it.
    /// </summary>
    Received = 3,

    /// <summary>
    /// The responsive HTML payload was produced by the template engine.
    /// </summary>
    Rendered = 4,

    /// <summary>
    /// The notification was handed to the delivery provider (SMTP relay, webhook).
    /// </summary>
    Dispatched = 5,

    /// <summary>
    /// Processing failed. Retry policies or the dead-letter queue take over.
    /// </summary>
    Failed = 6
}
