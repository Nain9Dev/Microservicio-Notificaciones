namespace Notificaciones.Infrastructure.Settings;

public class RabbitMqSettings
{
    public const string SectionName = "RabbitMq";
    public string Host { get; set; } = "localhost";
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string QueueName { get; set; } = "naindev-email-queue";
    public int RetryCount { get; set; } = 3;
    public int RetryIntervalSeconds { get; set; } = 2;

    /// <summary>
    /// Runs the whole pipeline on the MassTransit in-memory transport instead of RabbitMQ.
    /// Intended for single-process demos where no broker is available: the abstraction stays the
    /// same, only the transport changes. Never enable it in a multi-instance deployment, since
    /// in-memory messages never leave the process that published them.
    /// </summary>
    public bool UseInMemoryTransport { get; set; } = false;
}
