using System.ComponentModel.DataAnnotations;
using Notificaciones.Domain.Events;

namespace Notificaciones.Api.Models;

/// <summary>
/// Flexible DTO for testing different notification channels and presentation templates via Swagger UI demo.
/// </summary>
public class DemoNotificationRequest
{
    [Required]
    [EmailAddress]
    public string TargetRecipient { get; set; } = "evaluator@demo-test.com";

    [Required]
    public string Subject { get; set; } = "Demo: Clean Architecture Engine Live Test";

    [Required]
    public string Content { get; set; } = "Testing asynchronous notification dispatch across RabbitMQ cluster.";

    public NotificationType EventType { get; set; } = NotificationType.WelcomeMessage;

    public string Priority { get; set; } = "High";
}
