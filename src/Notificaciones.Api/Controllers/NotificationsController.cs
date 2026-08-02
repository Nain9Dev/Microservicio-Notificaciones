using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Notificaciones.Api.Models;
using Notificaciones.Domain.Events;

namespace Notificaciones.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class NotificationsController : ControllerBase
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(IPublishEndpoint publishEndpoint, ILogger<NotificationsController> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    /// <summary>
    /// Submits a contact form inquiry from naindev.com into the asynchronous notification queue.
    /// </summary>
    /// <param name="request">Contact form payload.</param>
    /// <returns>Accepted status with tracking ID.</returns>
    [HttpPost("contact")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitContactForm([FromBody] ContactFormRequest request, CancellationToken cancellationToken)
    {
        var notificationEvent = new NotificationEvent
        {
            NotificationId = Guid.NewGuid(),
            Recipient = "contact@naindev.com", // Delivered to NainDev inbox
            SenderName = request.SenderName,
            SenderEmail = request.SenderEmail,
            Subject = request.Subject,
            Body = request.Message,
            Type = NotificationType.ContactFormSubmission,
            Priority = "High",
            Timestamp = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, string>
            {
                ["Origin"] = "https://www.naindev.com/contact",
                ["ClientIp"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Anonymous",
                ["UserAgent"] = Request.Headers["User-Agent"].ToString()
            }
        };

        _logger.LogInformation("Publishing Contact Form submission [ID: {NotificationId}] from {SenderEmail} to RabbitMQ.", 
            notificationEvent.NotificationId, request.SenderEmail);

        await _publishEndpoint.Publish(notificationEvent, cancellationToken);

        return Accepted(new
        {
            Status = "Queued",
            Message = "Your inquiry has been received and scheduled for immediate processing.",
            TrackingId = notificationEvent.NotificationId,
            Timestamp = notificationEvent.Timestamp
        });
    }

    /// <summary>
    /// Interactive demonstration endpoint for testing custom notification templates via Swagger.
    /// </summary>
    /// <param name="request">Demo test parameter options.</param>
    /// <returns>Accepted status with full telemetry snapshot.</returns>
    [HttpPost("demo")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TriggerDemoDispatch([FromBody] DemoNotificationRequest request, CancellationToken cancellationToken)
    {
        var notificationEvent = new NotificationEvent
        {
            NotificationId = Guid.NewGuid(),
            Recipient = request.TargetRecipient,
            SenderName = "NainDev Demo Evaluator",
            SenderEmail = "demo@naindev.com",
            Subject = request.Subject,
            Body = request.Content,
            Type = request.EventType,
            Priority = request.Priority,
            Timestamp = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, string>
            {
                ["TriggeredBy"] = "Swagger Interactive Demo",
                ["Environment"] = "Live Portfolio Demo"
            }
        };

        _logger.LogInformation("Triggering Demo Dispatch [ID: {NotificationId}] Type: {Type} for {Recipient}.", 
            notificationEvent.NotificationId, notificationEvent.Type, request.TargetRecipient);

        await _publishEndpoint.Publish(notificationEvent, cancellationToken);

        return Accepted(new
        {
            Status = "Queued for Asynchronous Dispatch",
            Broker = "RabbitMQ Cluster",
            AssignedTrackingId = notificationEvent.NotificationId,
            TargetType = notificationEvent.Type.ToString(),
            EstimatedProcessingTimeMs = "< 500ms"
        });
    }

    /// <summary>
    /// Microservice health check status diagnostic endpoint.
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetHealthStatus()
    {
        return Ok(new
        {
            Service = "NainDev Cloud Notification API Gateway",
            Version = "1.0.0 (NET 10)",
            Architecture = "Clean Architecture + MassTransit",
            Status = "Healthy",
            Timestamp = DateTimeOffset.UtcNow
        });
    }
}
