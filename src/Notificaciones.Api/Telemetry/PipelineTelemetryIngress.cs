using Notificaciones.Domain.Events;

namespace Notificaciones.Api.Telemetry;

/// <summary>
/// Single entry point for pipeline telemetry: folds the event into the aggregated read model and
/// pushes it to every attached live console in one call, keeping both views consistent.
/// </summary>
public sealed class PipelineTelemetryIngress
{
    private readonly INotificationTelemetryStore _store;
    private readonly NotificationStreamBroadcaster _broadcaster;

    public PipelineTelemetryIngress(INotificationTelemetryStore store, NotificationStreamBroadcaster broadcaster)
    {
        _store = store;
        _broadcaster = broadcaster;
    }

    public void Ingest(NotificationLifecycleEvent lifecycleEvent)
    {
        _store.Record(lifecycleEvent);
        _broadcaster.Broadcast(lifecycleEvent);
    }
}
