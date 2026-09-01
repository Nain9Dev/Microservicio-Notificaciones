using System.ComponentModel.DataAnnotations;
using Notificaciones.Domain.Events;

namespace Notificaciones.Api.Models;

/// <summary>
/// Flexible DTO for exercising the different notification channels and presentation templates
/// from the live console or from the Swagger UI.
/// </summary>
public class DemoNotificationRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(254)]
    public string TargetRecipient { get; set; } = "evaluator@demo-test.com";

    [Required]
    [MaxLength(150)]
    public string Subject { get; set; } = "Demo: Clean Architecture Engine Live Test";

    [Required]
    [MaxLength(5000)]
    public string Content { get; set; } = "Testing asynchronous notification dispatch across the RabbitMQ cluster.";

    [EnumDataType(typeof(NotificationType), ErrorMessage = "Unsupported notification type.")]
    public NotificationType EventType { get; set; } = NotificationType.WelcomeMessage;

    [RegularExpression("^(High|Normal|Low)$", ErrorMessage = "Priority must be High, Normal or Low.")]
    public string Priority { get; set; } = "High";

    /// <summary>
    /// Optional display name used by the templates that greet the recipient.
    /// </summary>
    [MaxLength(100)]
    public string? SenderName { get; set; }
}
