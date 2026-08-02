namespace Notificaciones.Infrastructure.Settings;

public class RabbitMqSettings
{
    public const string SectionName = "RabbitMq";
    
    /// <summary>
    /// Optional AMQP/AMQPS Connection URI (e.g. from CloudAMQP: "amqps://user:password@host.rmq.cloudamqp.com/vhost").
    /// If provided, this overrides Host, VirtualHost, Username, and Password settings.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    public string Host { get; set; } = "localhost";
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string QueueName { get; set; } = "naindev-email-queue";
    public int RetryCount { get; set; } = 3;
    public int RetryIntervalSeconds { get; set; } = 2;
}
