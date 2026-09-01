using MassTransit;
using Notificaciones.Api.Telemetry;
using Notificaciones.Domain.Events;

namespace Notificaciones.Api.Consumers;

/// <summary>
/// Subscribes the gateway to the lifecycle events emitted by the worker fleet and folds them into
/// the live read model. It runs on a temporary, auto-deleted queue so every gateway instance gets
/// its own copy of the stream and never competes with the workers for notification messages.
/// </summary>
public class NotificationTelemetryConsumer : IConsumer<NotificationLifecycleEvent>
{
    private readonly PipelineTelemetryIngress _ingress;
    private readonly ILogger<NotificationTelemetryConsumer> _logger;

    public NotificationTelemetryConsumer(PipelineTelemetryIngress ingress, ILogger<NotificationTelemetryConsumer> logger)
    {
        _ingress = ingress;
        _logger = logger;
    }

    public Task Consume(ConsumeContext<NotificationLifecycleEvent> context)
    {
        var lifecycleEvent = context.Message;

        _logger.LogDebug(
            "[TELEMETRY] {Stage} for {NotificationId} from {Source} in {Elapsed}ms.",
            lifecycleEvent.Stage, lifecycleEvent.NotificationId, lifecycleEvent.Source, lifecycleEvent.ElapsedMilliseconds);

        _ingress.Ingest(lifecycleEvent);

        return Task.CompletedTask;
    }
}
