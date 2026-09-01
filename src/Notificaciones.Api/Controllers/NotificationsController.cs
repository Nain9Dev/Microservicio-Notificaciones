using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Notificaciones.Api.Models;
using Notificaciones.Api.Serialization;
using Notificaciones.Api.Settings;
using Notificaciones.Api.Telemetry;
using Notificaciones.Application.Services;
using Notificaciones.Domain.Events;

namespace Notificaciones.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class NotificationsController : ControllerBase
{
    private const string SourceName = "gateway";
    private const string ContactInbox = "contact@naindev.com";
    private static readonly TimeSpan PublishTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StreamHeartbeatInterval = TimeSpan.FromSeconds(15);

    private readonly IPublishEndpoint _publishEndpoint;
    private readonly EmailTemplateEngine _templateEngine;
    private readonly INotificationTelemetryStore _telemetry;
    private readonly NotificationStreamBroadcaster _broadcaster;
    private readonly PipelineTelemetryIngress _ingress;
    private readonly HealthCheckService _healthCheckService;
    private readonly DemoPolicySettings _demoPolicy;
    private readonly GatewayRuntimeInfo _runtimeInfo;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        IPublishEndpoint publishEndpoint,
        EmailTemplateEngine templateEngine,
        INotificationTelemetryStore telemetry,
        NotificationStreamBroadcaster broadcaster,
        PipelineTelemetryIngress ingress,
        HealthCheckService healthCheckService,
        IOptions<DemoPolicySettings> demoPolicy,
        GatewayRuntimeInfo runtimeInfo,
        ILogger<NotificationsController> logger)
    {
        _publishEndpoint = publishEndpoint;
        _templateEngine = templateEngine;
        _telemetry = telemetry;
        _broadcaster = broadcaster;
        _ingress = ingress;
        _healthCheckService = healthCheckService;
        _demoPolicy = demoPolicy.Value;
        _runtimeInfo = runtimeInfo;
        _logger = logger;
    }

    /// <summary>
    /// Submits a contact form inquiry from naindev.com into the asynchronous notification queue.
    /// </summary>
    [HttpPost("contact")]
    [EnableRateLimiting(RateLimitPolicies.ContactForm)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SubmitContactForm([FromBody] ContactFormRequest request, CancellationToken cancellationToken)
    {
        // Honeypot: a filled hidden field means an automated submission. Answer with the regular
        // accepted payload so the bot cannot tell the difference, but drop the message.
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            _logger.LogInformation("[ANTI-SPAM] Contact submission discarded by honeypot rule.");

            return Accepted(new
            {
                Status = "Queued",
                Message = "Your inquiry has been received and scheduled for immediate processing.",
                TrackingId = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        var notificationEvent = new NotificationEvent
        {
            NotificationId = Guid.NewGuid(),
            Recipient = ContactInbox,
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
                ["ClientIp"] = ResolveClientIp(),
                ["UserAgent"] = Truncate(Request.Headers.UserAgent.ToString(), 180)
            }
        };

        var dispatch = await PublishWithTelemetryAsync(notificationEvent, cancellationToken);

        if (!dispatch.Succeeded)
        {
            return BrokerUnavailable(dispatch.Error);
        }

        return Accepted(new
        {
            Status = "Queued",
            Message = "Your inquiry has been received and scheduled for immediate processing.",
            TrackingId = notificationEvent.NotificationId,
            Timestamp = notificationEvent.Timestamp
        });
    }

    /// <summary>
    /// Interactive demonstration endpoint used by the live console and the Swagger UI.
    /// </summary>
    [HttpPost("demo")]
    [EnableRateLimiting(RateLimitPolicies.DemoDispatch)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> TriggerDemoDispatch([FromBody] DemoNotificationRequest request, CancellationToken cancellationToken)
    {
        // Sandbox guard: never let a public endpoint choose an arbitrary delivery target.
        var (recipient, wasRedirected) = _demoPolicy.ResolveRecipient(request.TargetRecipient);

        var notificationEvent = new NotificationEvent
        {
            NotificationId = Guid.NewGuid(),
            Recipient = recipient,
            SenderName = string.IsNullOrWhiteSpace(request.SenderName) ? "NainDev Demo Evaluator" : request.SenderName,
            SenderEmail = "demo@naindev.com",
            Subject = request.Subject,
            Body = request.Content,
            Type = request.EventType,
            Priority = request.Priority,
            Timestamp = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, string>
            {
                ["TriggeredBy"] = "Live console",
                ["Environment"] = "Public portfolio demo",
                ["Sandboxed"] = wasRedirected ? "true" : "false"
            }
        };

        var dispatch = await PublishWithTelemetryAsync(notificationEvent, cancellationToken);

        if (!dispatch.Succeeded)
        {
            return BrokerUnavailable(dispatch.Error);
        }

        return Accepted(new
        {
            Status = "Queued for asynchronous dispatch",
            Broker = "RabbitMQ cluster",
            AssignedTrackingId = notificationEvent.NotificationId,
            TargetType = notificationEvent.Type.ToString(),
            EffectiveRecipient = PrivacyMasker.MaskEmail(recipient),
            Sandboxed = wasRedirected,
            SandboxNotice = wasRedirected
                ? "El destinatario solicitado no está en la lista permitida, la entrega se redirige al buzón interno de pruebas."
                : null
        });
    }

    /// <summary>
    /// Renders a notification through the template engine without touching the broker.
    /// Powers the live preview panel of the console.
    /// </summary>
    [HttpPost("preview")]
    [EnableRateLimiting(RateLimitPolicies.Preview)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult RenderPreview([FromBody] DemoNotificationRequest request)
    {
        var stopwatch = Stopwatch.StartNew();

        var previewEvent = new NotificationEvent
        {
            NotificationId = Guid.NewGuid(),
            Recipient = request.TargetRecipient,
            SenderName = string.IsNullOrWhiteSpace(request.SenderName) ? "NainDev Demo Evaluator" : request.SenderName,
            SenderEmail = "demo@naindev.com",
            Subject = request.Subject,
            Body = request.Content,
            Type = request.EventType,
            Priority = request.Priority,
            Timestamp = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, string>
            {
                ["Mode"] = "Preview",
                ["Engine"] = "EmailTemplateEngine"
            }
        };

        var rendered = new RenderedNotification(
            _templateEngine.GenerateHtmlTemplate(previewEvent),
            _templateEngine.GeneratePlainTextTemplate(previewEvent));

        stopwatch.Stop();

        return Ok(new
        {
            Html = rendered.Html,
            PlainText = rendered.PlainText,
            Bytes = rendered.HtmlBytes,
            RenderMilliseconds = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2),
            Type = previewEvent.Type.ToString()
        });
    }

    /// <summary>
    /// Live pipeline feed over Server-Sent Events. Emits a hydration frame on connect, then one
    /// frame per stage transition, plus a periodic heartbeat to survive intermediate proxies.
    /// </summary>
    [HttpGet("stream")]
    [Produces("text/event-stream")]
    public async Task StreamPipelineAsync()
    {
        using var subscription = _broadcaster.Subscribe();

        if (subscription is null)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await Response.WriteAsync("Live stream capacity reached.");
            return;
        }

        var cancellationToken = HttpContext.RequestAborted;

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache, no-store";
        Response.Headers.Connection = "keep-alive";
        // Disable response buffering on reverse proxies such as Nginx.
        Response.Headers["X-Accel-Buffering"] = "no";

        await WriteEventAsync("hydrate", new
        {
            Snapshot = _telemetry.GetSnapshot(),
            Recent = _telemetry.GetRecentEvents(40),
            Listeners = _broadcaster.SubscriberCount
        }, cancellationToken);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var heartbeatTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                heartbeatTimeout.CancelAfter(StreamHeartbeatInterval);

                try
                {
                    // WaitToReadAsync never consumes an item, so a heartbeat timeout cannot swallow events.
                    if (!await subscription.Reader.WaitToReadAsync(heartbeatTimeout.Token))
                    {
                        break;
                    }

                    while (subscription.Reader.TryRead(out var lifecycleEvent))
                    {
                        await WriteEventAsync("lifecycle", lifecycleEvent, cancellationToken);
                    }

                    await WriteEventAsync("metrics", _telemetry.GetSnapshot(), cancellationToken);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    await Response.WriteAsync(": heartbeat\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The browser navigated away or closed the tab. Nothing to clean up beyond the subscription.
        }
    }

    /// <summary>
    /// Aggregated metrics of the pipeline as observed by this gateway instance.
    /// </summary>
    [HttpGet("metrics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetMetrics() => Ok(new
    {
        Snapshot = _telemetry.GetSnapshot(),
        Listeners = _broadcaster.SubscriberCount
    });

    /// <summary>
    /// Most recent lifecycle transitions, newest first.
    /// </summary>
    [HttpGet("activity")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetActivity([FromQuery] int limit = 40) =>
        Ok(_telemetry.GetRecentEvents(Math.Clamp(limit, 1, 200)));

    /// <summary>
    /// Diagnostic endpoint reporting the real state of every registered dependency.
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHealthStatus(CancellationToken cancellationToken)
    {
        var report = await _healthCheckService.CheckHealthAsync(cancellationToken);

        var payload = new
        {
            Service = "NainDev Cloud Notification API Gateway",
            Version = "2.0.0 (.NET 10)",
            Architecture = "Clean Architecture + MassTransit",
            Status = report.Status.ToString(),
            Transport = _runtimeInfo.TransportMode,
            WorkerInProcess = _runtimeInfo.RunsWorkerInProcess,
            InstanceId = _runtimeInfo.InstanceId,
            UptimeSeconds = (long)(DateTimeOffset.UtcNow - _runtimeInfo.StartedAt).TotalSeconds,
            CheckDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            Dependencies = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    Status = entry.Value.Status.ToString(),
                    Description = entry.Value.Description,
                    DurationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 2)
                }),
            Timestamp = DateTimeOffset.UtcNow
        };

        return report.Status == HealthStatus.Unhealthy
            ? StatusCode(StatusCodes.Status503ServiceUnavailable, payload)
            : Ok(payload);
    }

    /// <summary>
    /// Publishes the event to the broker and records the gateway-side stages of its lifecycle.
    /// </summary>
    /// <remarks>
    /// The gateway records its own stages locally instead of publishing them: they would otherwise
    /// travel to the broker and come back through the telemetry consumer, double counting them.
    /// </remarks>
    private async Task<DispatchOutcome> PublishWithTelemetryAsync(NotificationEvent notificationEvent, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        _ingress.Ingest(BuildGatewayLifecycleEvent(
            notificationEvent, NotificationStage.Accepted, stopwatch, "Payload validado en el gateway"));

        try
        {
            using var publishTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            publishTimeout.CancelAfter(PublishTimeout);

            await _publishEndpoint.Publish(notificationEvent, publishTimeout.Token);
            stopwatch.Stop();

            _ingress.Ingest(BuildGatewayLifecycleEvent(
                notificationEvent, NotificationStage.Queued, stopwatch, "Publicado en el exchange del broker"));

            _logger.LogInformation(
                "[GATEWAY] Published {Type} [ID: {NotificationId}] in {Elapsed}ms.",
                notificationEvent.Type, notificationEvent.NotificationId, stopwatch.ElapsedMilliseconds);

            return DispatchOutcome.Success;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            var reason = ex is OperationCanceledException && !cancellationToken.IsCancellationRequested
                ? $"El broker no confirmó la publicación en {PublishTimeout.TotalSeconds:0} segundos"
                : ex.Message;

            _ingress.Ingest(BuildGatewayLifecycleEvent(
                notificationEvent, NotificationStage.Failed, stopwatch, "No se pudo publicar en el broker", reason));

            _logger.LogError(ex,
                "[GATEWAY] Failed to publish {Type} [ID: {NotificationId}].",
                notificationEvent.Type, notificationEvent.NotificationId);

            return DispatchOutcome.Failure(reason);
        }
    }

    private static NotificationLifecycleEvent BuildGatewayLifecycleEvent(
        NotificationEvent notificationEvent,
        NotificationStage stage,
        Stopwatch stopwatch,
        string detail,
        string? error = null) => new()
        {
            NotificationId = notificationEvent.NotificationId,
            Stage = stage,
            Type = notificationEvent.Type,
            Priority = notificationEvent.Priority,
            Recipient = PrivacyMasker.MaskEmail(notificationEvent.Recipient),
            Subject = notificationEvent.Subject,
            Source = SourceName,
            ElapsedMilliseconds = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2),
            Attempt = 1,
            Detail = detail,
            Error = error,
            Timestamp = DateTimeOffset.UtcNow
        };

    private async Task WriteEventAsync(string eventName, object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, ApiJson.Options);

        var frame = new StringBuilder()
            .Append("event: ").Append(eventName).Append('\n')
            .Append("data: ").Append(json).Append("\n\n")
            .ToString();

        await Response.WriteAsync(frame, cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private ObjectResult BrokerUnavailable(string? reason) =>
        StatusCode(StatusCodes.Status503ServiceUnavailable, new
        {
            Status = "Broker unavailable",
            Message = "La notificación no pudo encolarse. El bus de mensajería no está disponible.",
            Detail = reason,
            Timestamp = DateTimeOffset.UtcNow
        });

    private string ResolveClientIp() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static string Truncate(string value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];

    /// <summary>
    /// Result of a publish attempt, avoiding exceptions as control flow in the action methods.
    /// </summary>
    private readonly record struct DispatchOutcome(bool Succeeded, string? Error)
    {
        public static DispatchOutcome Success => new(true, null);

        public static DispatchOutcome Failure(string error) => new(false, error);
    }
}
