using Notificaciones.Domain.Events;

namespace Notificaciones.Application.Services;

/// <summary>
/// Abstraction for delivering notification events through real world communication channels (e.g. Email, Webhooks).
/// </summary>
public interface INotificationSender
{
    /// <summary>
    /// Dispatches the notification event formatted with the provided HTML body.
    /// </summary>
    /// <param name="notification">The immutable domain event.</param>
    /// <param name="htmlBody">The pre-rendered HTML template body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous dispatch operation.</returns>
    Task SendAsync(NotificationEvent notification, string htmlBody, CancellationToken cancellationToken = default);
}
