using System.Text;
using System.Text.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Notificaciones.Application.Services;
using Notificaciones.Domain.Events;
using Notificaciones.Infrastructure.Settings;

namespace Notificaciones.Infrastructure.Services;

/// <summary>
/// Notification delivery provider backed by SMTP (MailKit), with a failsafe simulation mode for
/// credential-less demo environments and an optional real-time webhook broadcast.
/// </summary>
public class SmtpEmailSender : INotificationSender
{
    private readonly NotificationSettings _emailSettings;
    private readonly WebhookSettings _webhookSettings;
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public SmtpEmailSender(
        IOptions<NotificationSettings> emailOptions,
        IOptions<WebhookSettings> webhookOptions,
        ILogger<SmtpEmailSender> logger,
        IHttpClientFactory httpClientFactory)
    {
        _emailSettings = emailOptions.Value;
        _webhookSettings = webhookOptions.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<NotificationDeliveryResult> SendAsync(
        NotificationEvent notification,
        RenderedNotification rendered,
        CancellationToken cancellationToken = default)
    {
        // 1. Primary channel: e-mail.
        var result = await DispatchEmailAsync(notification, rendered, cancellationToken);

        // 2. Secondary alerting channel, only when explicitly enabled.
        if (_webhookSettings.Enabled && !string.IsNullOrWhiteSpace(_webhookSettings.DiscordOrTelegramUrl))
        {
            await DispatchWebhookAlertAsync(notification, cancellationToken);
        }

        return result;
    }

    private async Task<NotificationDeliveryResult> DispatchEmailAsync(
        NotificationEvent notification,
        RenderedNotification rendered,
        CancellationToken cancellationToken)
    {
        var maskedRecipient = PrivacyMasker.MaskEmail(notification.Recipient);

        // Without credentials there is nothing to authenticate with: stay in simulation mode
        // so the public demo keeps working instead of failing on every message.
        if (string.IsNullOrWhiteSpace(_emailSettings.Username) && _emailSettings.EnableDemoSimulationMode)
        {
            _logger.LogWarning(
                "[SIMULATION MODE] No SMTP credentials configured. Simulating dispatch for {Recipient}.",
                maskedRecipient);
            _logger.LogInformation(
                "[SIMULATED PAYLOAD] FROM: {From} | TO: {To} | SUBJECT: {Subject} | HTML: {Size} bytes",
                _emailSettings.DefaultFromEmail, maskedRecipient, notification.Subject, rendered.HtmlBytes);

            await Task.Delay(150, cancellationToken); // Approximate a realistic relay round trip.
            return NotificationDeliveryResult.Simulation("Simulado: no hay credenciales SMTP configuradas");
        }

        var message = BuildMimeMessage(notification, rendered);

        try
        {
            using var client = new SmtpClient();
            var secureSocketOptions = _emailSettings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto;

            await client.ConnectAsync(_emailSettings.SmtpHost, _emailSettings.SmtpPort, secureSocketOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_emailSettings.Username) && !string.IsNullOrWhiteSpace(_emailSettings.Password))
            {
                await client.AuthenticateAsync(_emailSettings.Username, _emailSettings.Password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation(
                "[SMTP SUCCESS] Delivered to relay {Host}:{Port} for recipient {Recipient}.",
                _emailSettings.SmtpHost, _emailSettings.SmtpPort, maskedRecipient);

            return NotificationDeliveryResult.Smtp(_emailSettings.SmtpHost, _emailSettings.SmtpPort);
        }
        catch (Exception ex)
        {
            if (_emailSettings.EnableDemoSimulationMode)
            {
                _logger.LogWarning(ex,
                    "[SMTP FALLBACK] Delivery to {Host}:{Port} failed. Falling back to simulation to keep the demo alive.",
                    _emailSettings.SmtpHost, _emailSettings.SmtpPort);

                return NotificationDeliveryResult.Simulation($"Fallback simulado: el relay SMTP no respondió ({ex.GetType().Name})");
            }

            _logger.LogError(ex, "[SMTP FATAL] Failed to send email to {Recipient}.", maskedRecipient);
            throw;
        }
    }

    /// <summary>
    /// Assembles the multipart message. Providing a plain text alternative alongside the HTML
    /// body measurably improves inbox placement.
    /// </summary>
    private MimeMessage BuildMimeMessage(NotificationEvent notification, RenderedNotification rendered)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_emailSettings.DefaultFromName, _emailSettings.DefaultFromEmail));
        message.To.Add(MailboxAddress.Parse(notification.Recipient));
        message.Subject = notification.Subject;

        // Let the operator reply straight to the visitor when the event comes from the contact form.
        if (notification.Type == NotificationType.ContactFormSubmission &&
            !string.IsNullOrWhiteSpace(notification.SenderEmail) &&
            MailboxAddress.TryParse(notification.SenderEmail, out var replyTo))
        {
            message.ReplyTo.Add(replyTo);
        }

        message.Headers.Add("X-Notification-Id", notification.NotificationId.ToString());
        message.Headers.Add("X-Notification-Type", notification.Type.ToString());

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = rendered.Html,
            TextBody = rendered.PlainText
        };

        message.Body = bodyBuilder.ToMessageBody();
        return message;
    }

    private async Task DispatchWebhookAlertAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("WebhookClient");

            // Lightweight payload compatible with Discord and Telegram bot webhook parsers.
            var payload = new
            {
                content = string.Join('\n',
                    "**[NAINDEV CLOUD NOTIFICATION]**",
                    $"**Type:** `{notification.Type}`",
                    $"**From:** `{notification.SenderName}` (`{PrivacyMasker.MaskEmail(notification.SenderEmail)}`)",
                    $"**Subject:** {notification.Subject}",
                    $"**Tracking:** `{notification.NotificationId}`")
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(_webhookSettings.DiscordOrTelegramUrl, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[WEBHOOK SUCCESS] Secondary alert dispatched.");
            }
            else
            {
                _logger.LogWarning("[WEBHOOK WARN] Endpoint answered {StatusCode}.", (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[WEBHOOK WARN] Secondary alert failed. Primary notification stream unaffected.");
        }
    }
}
