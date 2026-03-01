namespace Notificaciones.Domain.Events;

public record NotificationEvent
{
    public string Recipient { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
}