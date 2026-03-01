using MassTransit;
using Notificaciones.Domain.Events;

namespace Notificaciones.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IBus _bus;

    public Worker(ILogger<Worker> logger, IBus bus)
    {
        _logger = logger;
        _bus = bus;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var notification = new NotificationEvent
            {
                Recipient = "contact@naindev.com",
                Subject = "System Alert: Microservice Online",
                Body = "The RabbitMQ integration is working perfectly."
            };

            await _bus.Publish(notification, stoppingToken);
            _logger.LogInformation("--> Message published to the bus at {time}", DateTimeOffset.Now);

            await Task.Delay(5000, stoppingToken);
        }
    }
}