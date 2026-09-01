using Notificaciones.Domain.Events;

namespace Notificaciones.Application.Services;

/// <summary>
/// Abstraction for delivering notification events through real world communication channels (e.g. Email, Webhooks).
/// </summary>
public interface INotificationSender
{
    /// <summary>
    /// Dispatches the notification event using the pre-rendered multipart payload.
    /// </summary>
    /// <param name="notification">The immutable domain event.</param>
    /// <param name="rendered">The HTML and plain text representations produced by the template engine.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The delivery outcome reported by the provider.</returns>
    Task<NotificationDeliveryResult> SendAsync(NotificationEvent notification, RenderedNotification rendered, CancellationToken cancellationToken = default);
}
