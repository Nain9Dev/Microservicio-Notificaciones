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
/// Enterprise notification delivery service supporting SMTP (via MailKit), demo logging fallbacks, and real-time webhook broadcasts.
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

    public async Task SendAsync(NotificationEvent notification, string htmlBody, CancellationToken cancellationToken = default)
    {
        // 1. Send via SMTP / Email Channel
        await DispatchEmailAsync(notification, htmlBody, cancellationToken);

        // 2. Dispatch secondary alerting via Webhooks if enabled or on Contact Form submission
        if (_webhookSettings.Enabled && !string.IsNullOrWhiteSpace(_webhookSettings.DiscordOrTelegramUrl))
        {
            await DispatchWebhookAlertAsync(notification, cancellationToken);
        }
    }

    private async Task DispatchEmailAsync(NotificationEvent notification, string htmlBody, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_emailSettings.DefaultFromName, _emailSettings.DefaultFromEmail));
        message.To.Add(new MailboxAddress(notification.Recipient, notification.Recipient));
        message.Subject = notification.Subject;

        var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
        message.Body = bodyBuilder.ToMessageBody();

        // If no credentials are configured or explicit demo mode is set and host is mailtrap/localhost without credentials
        if (string.IsNullOrWhiteSpace(_emailSettings.Username) && _emailSettings.EnableDemoSimulationMode)
        {
            _logger.LogWarning(" [DEMO / FAILSAFE MODE] No SMTP credentials detected in settings. Simulating Email Dispatch for [{Recipient}] without contacting SMTP server.", notification.Recipient);
            _logger.LogInformation("--- [SIMULATED EMAIL HEADERS] ---");
            _logger.LogInformation("FROM: {From} | TO: {To} | SUBJECT: {Subject}", _emailSettings.DefaultFromEmail, notification.Recipient, notification.Subject);
            _logger.LogInformation("BODY SIZE: {Size} bytes of responsive HTML generated successfully.", htmlBody.Length);
            await Task.Delay(150, cancellationToken); // Simulate real network latency
            return;
        }

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

            _logger.LogInformation(" [SMTP DISPATCH SUCCESS] Email successfully delivered to SMTP relay [{Host}:{Port}] for recipient: {Recipient}", _emailSettings.SmtpHost, _emailSettings.SmtpPort, notification.Recipient);
        }
        catch (Exception ex)
        {
            if (_emailSettings.EnableDemoSimulationMode)
            {
                _logger.LogWarning(ex, " [SMTP FAILSAFE DEMO FALLBACK] SMTP delivery failed to [{Host}:{Port}]. Switching to Demo Simulation log mode to preserve portfolio functionality.", _emailSettings.SmtpHost, _emailSettings.SmtpPort);
                _logger.LogInformation("--- [SIMULATED EMAIL PAYLOAD SAVED] TO: {Recipient} | SUBJECT: {Subject}", notification.Recipient, notification.Subject);
            }
            else
            {
                _logger.LogError(ex, " [SMTP FATAL ERROR] Failed to send email to {Recipient}", notification.Recipient);
                throw;
            }
        }
    }

    private async Task DispatchWebhookAlertAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("WebhookClient");
            
            // Format lightweight payload compatible with Discord/Telegram bot webhook parsers
            var payload = new
            {
                content = $"🚀 **[NAINDEV CLOUD NOTIFICATION]**\n**Type:** `{notification.Type}`\n**From:** `{notification.SenderName}` (`{notification.SenderEmail}`)\n**Subject:** {notification.Subject}\n**Body:** {notification.Body}"
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(_webhookSettings.DiscordOrTelegramUrl, content, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(" [WEBHOOK SUCCESS] Instant mobile notification dispatched via Webhook.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, " [WEBHOOK WARN] Failed to dispatch secondary webhook alert. Primary notification stream unhindered.");
        }
    }
}
