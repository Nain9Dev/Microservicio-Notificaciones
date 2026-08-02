namespace Notificaciones.Infrastructure.Settings;

public class NotificationSettings
{
    public const string SectionName = "NotificationSettings";
    public string SmtpHost { get; set; } = "smtp.mailtrap.io";
    public int SmtpPort { get; set; } = 2525;
    public bool UseSsl { get; set; } = false;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DefaultFromEmail { get; set; } = "notifications@naindev.com";
    public string DefaultFromName { get; set; } = "NainDev Cloud Engine";
    public bool EnableDemoSimulationMode { get; set; } = true;
}
