namespace Notificaciones.Domain.Events;

/// <summary>
/// Observability contract broadcast every time a notification advances through the pipeline.
/// It is intentionally decoupled from <see cref="NotificationEvent"/>: telemetry subscribers
/// never receive the message body, only the metadata required to trace and measure the flow.
/// </summary>
public record NotificationLifecycleEvent
{
    /// <summary>
    /// Correlation identifier shared with the originating <see cref="NotificationEvent"/>.
    /// </summary>
    public Guid NotificationId { get; init; }

    /// <summary>
    /// Stage reached by the notification when this event was emitted.
    /// </summary>
    public NotificationStage Stage { get; init; }

    /// <summary>
    /// Categorical type of the originating notification.
    /// </summary>
    public NotificationType Type { get; init; }

    /// <summary>
    /// Priority assigned by the producer.
    /// </summary>
    public string Priority { get; init; } = "Normal";

    /// <summary>
    /// Target recipient, masked by the producer when the value is publicly exposed.
    /// </summary>
    public string Recipient { get; init; } = string.Empty;

    /// <summary>
    /// Subject line of the originating notification.
    /// </summary>
    public string Subject { get; init; } = string.Empty;

    /// <summary>
    /// Logical name of the component that emitted the event (gateway, worker, ...).
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Time elapsed inside the emitting component up to this stage, in milliseconds.
    /// </summary>
    public double ElapsedMilliseconds { get; init; }

    /// <summary>
    /// Size in bytes of the rendered HTML payload. Zero before the rendering stage.
    /// </summary>
    public int PayloadBytes { get; init; }

    /// <summary>
    /// Number of delivery attempts performed by the broker retry policy.
    /// </summary>
    public int Attempt { get; init; }

    /// <summary>
    /// Effective delivery channel resolved by the provider (smtp, simulation, webhook).
    /// Empty for stages that happen before the dispatch step.
    /// </summary>
    public string Channel { get; init; } = string.Empty;

    /// <summary>
    /// Human readable detail describing the stage transition.
    /// </summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>
    /// Failure reason. Only populated when <see cref="Stage"/> is <see cref="NotificationStage.Failed"/>.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// UTC instant the stage transition happened.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
