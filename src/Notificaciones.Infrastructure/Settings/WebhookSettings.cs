namespace Notificaciones.Infrastructure.Settings;

public class WebhookSettings
{
    public const string SectionName = "WebhookSettings";
    public bool Enabled { get; set; } = false;
    public string DiscordOrTelegramUrl { get; set; } = string.Empty;
}
