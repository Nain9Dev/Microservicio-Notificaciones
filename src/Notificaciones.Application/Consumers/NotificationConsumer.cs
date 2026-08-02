using System.Diagnostics;
using MassTransit;
using Microsoft.Extensions.Logging;
using Notificaciones.Application.Services;
using Notificaciones.Domain.Events;

namespace Notificaciones.Application.Consumers;

public class NotificationConsumer : IConsumer<NotificationEvent>
{
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
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "====> [CONSUMER START] Received {NotificationType} event [ID: {NotificationId}] for Recipient: {Recipient}",
            notification.Type, notification.NotificationId, notification.Recipient);

        try
        {
            // 1. Generate customized responsive HTML template
            string htmlPayload = _templateEngine.GenerateHtmlTemplate(notification);

            // 2. Dispatch notification via configured infrastructure provider
            await _sender.SendAsync(notification, htmlPayload, context.CancellationToken);

            stopwatch.Stop();
            _logger.LogInformation(
                "<==== [CONSUMER SUCCESS] Dispatched {NotificationType} [ID: {NotificationId}] in {ElapsedMilliseconds}ms.",
                notification.Type, notification.NotificationId, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "[CONSUMER FAILURE] Error dispatching notification [ID: {NotificationId}] after {ElapsedMilliseconds}ms. Error: {Message}",
                notification.NotificationId, stopwatch.ElapsedMilliseconds, ex.Message);

            // Rethrowing allows MassTransit retry policies / Dead-Letter Queue handling to engage
            throw;
        }
    }
}