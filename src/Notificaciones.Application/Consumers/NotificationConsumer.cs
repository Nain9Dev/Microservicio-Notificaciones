using MassTransit;
using Microsoft.Extensions.Logging;
using Notificaciones.Domain.Events;

namespace Notificaciones.Application.Consumers;

public class NotificationConsumer : IConsumer<NotificationEvent>
{
    private readonly ILogger<NotificationConsumer> _logger;

    public NotificationConsumer(ILogger<NotificationConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<NotificationEvent> context)
    {
        var notification = context.Message;

        // Simulating email dispatch
        _logger.LogInformation("Processing email dispatch to: {Recipient}", notification.Recipient);
        _logger.LogInformation("Subject: {Subject}", notification.Subject);

        return Task.CompletedTask;
    }
}