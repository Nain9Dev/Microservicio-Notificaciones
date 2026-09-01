using System.Diagnostics;
using MassTransit;
using Microsoft.Extensions.Logging;
using Notificaciones.Application.Services;
using Notificaciones.Domain.Events;

namespace Notificaciones.Application.Consumers;

/// <summary>
/// Processes queued notification events: renders the multipart payload and hands it to the
/// delivery provider. Every stage transition is published as a <see cref="NotificationLifecycleEvent"/>
/// so the gateway can project a live view of the pipeline without coupling to this component.
/// </summary>
public class NotificationConsumer : IConsumer<NotificationEvent>
{
    private const string SourceName = "worker";

    private readonly ILogger<NotificationConsumer> _logger;
    private readonly INotificationSender _sender;
    private readonly EmailTemplateEngine _templateEngine;

    public NotificationConsumer(
        ILogger<NotificationConsumer> logger,
        INotificationSender sender,
        EmailTemplateEngine templateEngine)
    {
        _logger = logger;
        _sender = sender;
        _templateEngine = templateEngine;
    }

    public async Task Consume(ConsumeContext<NotificationEvent> context)
    {
        var notification = context.Message;
        var attempt = context.GetRetryAttempt() + 1;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "[CONSUMER START] Received {NotificationType} event [ID: {NotificationId}] attempt {Attempt} for recipient {Recipient}",
            notification.Type, notification.NotificationId, attempt, PrivacyMasker.MaskEmail(notification.Recipient));

        await PublishLifecycleAsync(context, notification, NotificationStage.Received, attempt, stopwatch,
            detail: attempt > 1 ? $"Reintento {attempt} tomado de la cola" : "Mensaje tomado de la cola");

        try
        {
            // 1. Generate the customized responsive HTML payload and its plain text alternative.
            var rendered = new RenderedNotification(
                _templateEngine.GenerateHtmlTemplate(notification),
                _templateEngine.GeneratePlainTextTemplate(notification));

            await PublishLifecycleAsync(context, notification, NotificationStage.Rendered, attempt, stopwatch,
                detail: "Plantilla responsive generada", payloadBytes: rendered.HtmlBytes);

            // 2. Dispatch the notification through the configured infrastructure provider.
            var delivery = await _sender.SendAsync(notification, rendered, context.CancellationToken);

            stopwatch.Stop();

            await PublishLifecycleAsync(context, notification, NotificationStage.Dispatched, attempt, stopwatch,
                detail: delivery.Detail, payloadBytes: rendered.HtmlBytes, channel: delivery.Channel);

            _logger.LogInformation(
                "[CONSUMER SUCCESS] Dispatched {NotificationType} [ID: {NotificationId}] in {ElapsedMilliseconds}ms.",
                notification.Type, notification.NotificationId, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            await PublishLifecycleAsync(context, notification, NotificationStage.Failed, attempt, stopwatch,
                detail: "Fallo en el procesamiento, se activa la política de reintentos", error: ex.Message);

            _logger.LogError(ex,
                "[CONSUMER FAILURE] Error dispatching notification [ID: {NotificationId}] after {ElapsedMilliseconds}ms.",
                notification.NotificationId, stopwatch.ElapsedMilliseconds);

            // Rethrowing allows MassTransit retry policies and dead-letter handling to engage.
            throw;
        }
    }

    /// <summary>
    /// Broadcasts a stage transition. Telemetry must never break the processing pipeline, so any
    /// publishing failure is logged and swallowed.
    /// </summary>
    private async Task PublishLifecycleAsync(
        ConsumeContext<NotificationEvent> context,
        NotificationEvent notification,
        NotificationStage stage,
        int attempt,
        Stopwatch stopwatch,
        string detail,
        int payloadBytes = 0,
        string? error = null,
        string channel = "")
    {
        try
        {
            await context.Publish(new NotificationLifecycleEvent
            {
                NotificationId = notification.NotificationId,
                Stage = stage,
                Type = notification.Type,
                Priority = notification.Priority,
                Recipient = PrivacyMasker.MaskEmail(notification.Recipient),
                Subject = notification.Subject,
                Source = SourceName,
                ElapsedMilliseconds = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2),
                PayloadBytes = payloadBytes,
                Attempt = attempt,
                Channel = channel,
                Detail = detail,
                Error = error,
                Timestamp = DateTimeOffset.UtcNow
            }, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[TELEMETRY WARN] Could not publish stage {Stage} for notification {NotificationId}.",
                stage, notification.NotificationId);
        }
    }
}
