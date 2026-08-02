using Notificaciones.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Notificaciones.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly RabbitMqSettings _rabbitSettings;
    private readonly NotificationSettings _notificationSettings;

    public Worker(
        ILogger<Worker> logger,
        IOptions<RabbitMqSettings> rabbitOptions,
        IOptions<NotificationSettings> notificationOptions)
    {
        _logger = logger;
        _rabbitSettings = rabbitOptions.Value;
        _notificationSettings = notificationOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("==================================================================");
        _logger.LogInformation("   NAINDEV CLOUD NOTIFICATION MICROSERVICE (WORKER SERVICE)");
        _logger.LogInformation("   Architecture: Clean Architecture + DDD + MassTransit (.NET 10)");
        _logger.LogInformation("   Broker Host: {Host} | Target Queue: {Queue}", _rabbitSettings.Host, _rabbitSettings.QueueName);
        _logger.LogInformation("   SMTP Relay: {SmtpHost}:{SmtpPort} | Failsafe Demo Mode: {DemoMode}", _notificationSettings.SmtpHost, _notificationSettings.SmtpPort, _notificationSettings.EnableDemoSimulationMode);
        _logger.LogInformation("==================================================================");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug(" [HEARTBEAT] Worker service is healthy and actively consuming queue [{QueueName}] at {Time}", 
                _rabbitSettings.QueueName, DateTimeOffset.Now);

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}