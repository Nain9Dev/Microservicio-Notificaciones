using System.Text;
using Notificaciones.Domain.Events;

namespace Notificaciones.Application.Services;

/// <summary>
/// Responsive HTML email template generator featuring modern, premium visual design tailored for naindev.com.
/// </summary>
public class EmailTemplateEngine
{
    /// <summary>
    /// Renders a full HTML email payload based on the notification type and event details.
    /// </summary>
    public string GenerateHtmlTemplate(NotificationEvent notification)
    {
        var contentHtml = notification.Type switch
        {
            NotificationType.ContactFormSubmission => RenderContactFormContent(notification),
            NotificationType.WelcomeMessage => RenderWelcomeContent(notification),
            NotificationType.SystemAlert => RenderSystemAlertContent(notification),
            _ => RenderGenericContent(notification)
        };

        return WrapInLayout(notification.Subject, contentHtml, notification.Type.ToString(), notification.Priority);
    }

    private string WrapInLayout(string windowTitle, string bodyContent, string badgeText, string priority)
    {
        var badgeColor = badgeText switch
        {
            "ContactFormSubmission" => "#38BDF8", // Cyan
            "WelcomeMessage" => "#10B981",        // Emerald
            "SystemAlert" => "#EF4444",           // Red/Alert
            _ => "#818CF8"                        // Indigo
        };

        return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <title>{windowTitle}</title>
</head>
<body style=""margin: 0; padding: 0; background-color: #0B0F19; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; color: #F1F5F9;"">
    <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""background-color: #0B0F19; padding: 40px 15px;"">
        <tr>
            <td align=""center"">
                <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""600"" style=""max-width: 600px; width: 100%; background-color: #1E293B; border-radius: 12px; overflow: hidden; box-shadow: 0 10px 25px rgba(0, 0, 0, 0.5); border: 1px solid #334155;"">
                    <!-- Header -->
                    <tr>
                        <td align=""center"" style=""padding: 30px 20px; background: linear-gradient(135deg, #0F172A 0%, #1E1B4B 100%); border-bottom: 2px solid {badgeColor};"">
                            <h1 style=""margin: 0; font-size: 28px; font-weight: 800; color: #FFFFFF; letter-spacing: 1px;"">
                                NAIN<span style=""color: #38BDF8;"">DEV</span> <span style=""font-size: 14px; color: #94A3B8; font-weight: 400; display: block; margin-top: 5px;"">Cloud Notification Service</span>
                            </h1>
                        </td>
                    </tr>
                    
                    <!-- Meta Badge Strip -->
                    <tr>
                        <td style=""padding: 15px 30px; background-color: #0F172A; font-size: 12px; color: #94A3B8; border-bottom: 1px solid #334155;"">
                            <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
                                <tr>
                                    <td align=""left"">
                                        <span style=""background-color: {badgeColor}20; color: {badgeColor}; padding: 4px 10px; border-radius: 20px; font-weight: bold; border: 1px solid {badgeColor};"">{badgeText}</span>
                                    </td>
                                    <td align=""right"">
                                        <strong>Priority:</strong> <span style=""color: #F8FAFC;"">{priority}</span>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Body Content -->
                    <tr>
                        <td style=""padding: 35px 30px; color: #E2E8F0; font-size: 15px; line-height: 1.6;"">
                            {bodyContent}
                        </td>
                    </tr>

                    <!-- Footer -->
                    <tr>
                        <td align=""center"" style=""padding: 25px 30px; background-color: #0F172A; font-size: 12px; color: #64748B; border-top: 1px solid #334155;"">
                            <p style=""margin: 0 0 10px 0;"">
                                Esta es una notificación automatizada procesada vía <strong>RabbitMQ &amp; MassTransit</strong> en .NET 10.
                            </p>
                            <p style=""margin: 0;"">
                                <a href=""https://www.naindev.com"" style=""color: #38BDF8; text-decoration: none; font-weight: bold;"">www.naindev.com</a> &bull; Clean Architecture Engine &bull; &copy; {DateTime.UtcNow.Year} NainDev
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private string RenderContactFormContent(NotificationEvent ev)
    {
        return $@"
<h2 style=""color: #FFFFFF; margin-top: 0; margin-bottom: 20px; font-size: 22px; font-weight: 600;"">
    💬 Nuevo Mensaje desde el Portafolio Web
</h2>
<p style=""margin-bottom: 20px; color: #94A3B8;"">
    Un visitante ha enviado una consulta a través de tu formulario de contacto en <strong>naindev.com</strong>:
</p>
<table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""background-color: #0F172A; border-radius: 8px; border: 1px solid #334155; margin-bottom: 25px;"">
    <tr>
        <td style=""padding: 15px 20px; border-bottom: 1px solid #1E293B;"">
            <span style=""color: #64748B; font-size: 13px; display: block;"">Remitente:</span>
            <strong style=""color: #F8FAFC; font-size: 16px;"">{ev.SenderName}</strong>
        </td>
    </tr>
    <tr>
        <td style=""padding: 15px 20px; border-bottom: 1px solid #1E293B;"">
            <span style=""color: #64748B; font-size: 13px; display: block;"">Correo Electrónico:</span>
            <a href=""mailto:{ev.SenderEmail}"" style=""color: #38BDF8; text-decoration: none; font-weight: 500;"">{ev.SenderEmail}</a>
        </td>
    </tr>
    <tr>
        <td style=""padding: 15px 20px;"">
            <span style=""color: #64748B; font-size: 13px; display: block;"">Asunto:</span>
            <strong style=""color: #F8FAFC;"">{ev.Subject}</strong>
        </td>
    </tr>
</table>
<div style=""background-color: #0B0F19; border-left: 4px solid #38BDF8; padding: 20px; border-radius: 0 8px 8px 0; color: #F1F5F9; font-size: 15px; white-space: pre-line; box-shadow: inset 0 2px 4px rgba(0,0,0,0.3);"">
{ev.Body}
</div>
<div style=""margin-top: 25px; font-size: 13px; color: #64748B;"">
    <span>ID de Tracking: <code>{ev.NotificationId}</code></span> &bull; 
    <span>Recibido: {ev.Timestamp:yyyy-MM-dd HH:mm:ss} UTC</span>
</div>";
    }

    private string RenderWelcomeContent(NotificationEvent ev)
    {
        return $@"
<h2 style=""color: #10B981; margin-top: 0; margin-bottom: 15px; font-size: 24px; font-weight: 700;"">
    ¡Bienvenido/a al ecosistema de NainDev! 🚀
</h2>
<p style=""font-size: 16px; color: #E2E8F0; margin-bottom: 20px;"">
    Hola <strong>{(string.IsNullOrEmpty(ev.SenderName) ? "Dev" : ev.SenderName)}</strong>,
</p>
<p style=""margin-bottom: 25px; color: #94A3B8; line-height: 1.7;"">
    {ev.Body}
</p>
<table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""margin: 30px 0;"">
    <tr>
        <td align=""center"">
            <a href=""https://www.naindev.com"" style=""display: inline-block; background: linear-gradient(90deg, #10B981 0%, #059669 100%); color: #FFFFFF; font-weight: bold; font-size: 16px; padding: 14px 32px; border-radius: 8px; text-decoration: none; box-shadow: 0 4px 12px rgba(16, 185, 129, 0.3);"">
                Explorar el Portafolio Web
            </a>
        </td>
    </tr>
</table>
<p style=""font-size: 14px; color: #64748B; margin-top: 30px;"">
    Si recibiste este correo como parte de nuestra demostración técnica del Microservicio de Notificaciones (.NET 10 &amp; RabbitMQ), tu prueba ha concluido con éxito.
</p>";
    }

    private string RenderSystemAlertContent(NotificationEvent ev)
    {
        return $@"
<div style=""background-color: #7F1D1D20; border: 1px solid #EF4444; padding: 15px 20px; border-radius: 8px; margin-bottom: 20px; display: flex; align-items: center;"">
    <strong style=""color: #FCA5A5; font-size: 16px;"">⚠️ ALERTA DE SISTEMA CLOUD</strong>
</div>
<h3 style=""color: #FFFFFF; margin-top: 0; margin-bottom: 15px;"">{ev.Subject}</h3>
<div style=""background-color: #0F172A; padding: 20px; border-radius: 8px; font-family: 'Courier New', monospace; font-size: 14px; color: #CBD5E1; border: 1px solid #334155; overflow-x: auto;"">
{ev.Body}
</div>
<p style=""margin-top: 20px; font-size: 13px; color: #94A3B8;"">
    Timestamp del Broker: {ev.Timestamp:u}<br />
    Destinatario Configurado: {ev.Recipient}
</p>";
    }

    private string RenderGenericContent(NotificationEvent ev)
    {
        return $@"
<h2 style=""color: #FFFFFF; margin-top: 0; margin-bottom: 15px; font-size: 22px;"">
    {ev.Subject}
</h2>
<p style=""color: #94A3B8; font-size: 13px; margin-bottom: 20px;"">
    Tipo de evento: <strong style=""color: #818CF8;"">{ev.Type}</strong> &bull; ID: <code>{ev.NotificationId}</code>
</p>
<div style=""background-color: #0F172A; padding: 25px; border-radius: 8px; border: 1px solid #334155; color: #F1F5F9; font-size: 15px; line-height: 1.6;"">
    {ev.Body}
</div>
{(ev.Metadata.Count > 0 ? RenderMetadataTable(ev.Metadata) : "")}";
    }

    private string RenderMetadataTable(Dictionary<string, string> metadata)
    {
        var sb = new StringBuilder();
        sb.Append(@"
<div style=""margin-top: 25px;"">
    <strong style=""color: #94A3B8; font-size: 13px; display: block; margin-bottom: 10px;"">Metadatos del Evento:</strong>
    <table border=""0"" cellpadding=""6"" cellspacing=""0"" width=""100%"" style=""background-color: #0B0F19; font-size: 12px; border-radius: 6px; border: 1px solid #1E293B;"">");
        foreach (var kvp in metadata)
        {
            sb.Append($@"
        <tr>
            <td style=""color: #64748B; width: 30%; border-bottom: 1px solid #1E293B; padding: 8px 12px;""><strong>{kvp.Key}</strong></td>
            <td style=""color: #CBD5E1; border-bottom: 1px solid #1E293B; padding: 8px 12px;""><code>{kvp.Value}</code></td>
        </tr>");
        }
        sb.Append(@"
    </table>
</div>");
        return sb.ToString();
    }
}
