using Notificaciones.Domain.Events;

namespace Notificaciones.Api.Telemetry;

/// <summary>
/// Read model fed by the lifecycle events emitted across the pipeline.
/// Implementations must be safe for concurrent access from broker consumers and HTTP requests.
/// </summary>
public interface INotificationTelemetryStore
{
    /// <summary>
    /// Folds a lifecycle event into the aggregated counters and the recent activity buffer.
    /// </summary>
    void Record(NotificationLifecycleEvent lifecycleEvent);

    /// <summary>
    /// Returns the most recent lifecycle events, newest first.
    /// </summary>
    IReadOnlyList<NotificationLifecycleEvent> GetRecentEvents(int limit);

    /// <summary>
    /// Builds the current aggregated snapshot.
    /// </summary>
    TelemetrySnapshot GetSnapshot();
}
