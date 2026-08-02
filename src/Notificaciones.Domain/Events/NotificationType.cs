namespace Notificaciones.Domain.Events;

/// <summary>
/// Defines the different types of notification events supported by the NainDev platform and demo integrations.
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// Message originating from the Contact Form on naindev.com
    /// </summary>
    ContactFormSubmission = 1,

    /// <summary>
    /// Automated welcome or confirmation email sent to visitors or subscribers
    /// </summary>
    WelcomeMessage = 2,

    /// <summary>
    /// Internal system alerting or operational notification
    /// </summary>
    SystemAlert = 3,

    /// <summary>
    /// Interactive demonstration test triggered via API or Swagger documentation
    /// </summary>
    TestDemo = 4
}
