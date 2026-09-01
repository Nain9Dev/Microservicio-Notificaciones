using System.Net;
using System.Text;
using Notificaciones.Domain.Events;

namespace Notificaciones.Application.Services;

/// <summary>
/// Renders responsive, client-hardened HTML e-mails for the NainDev notification platform.
/// The markup is table based and fully inlined so it survives Outlook, Gmail and Apple Mail,
/// and every value coming from the outside world is HTML encoded before being injected.
/// </summary>
public sealed class EmailTemplateEngine
{
    private const int MaxSubjectLength = 150;

    /// <summary>
    /// Visual identity applied to a notification type: accent colour, soft tint and section label.
    /// </summary>
    private readonly record struct TemplateTheme(string Accent, string AccentSoft, string AccentDeep);

    private static readonly TemplateTheme ContactTheme = new("#38BDF8", "#0E2A3D", "#0B4A6F");
    private static readonly TemplateTheme WelcomeTheme = new("#10B981", "#0B2E24", "#065F46");
    private static readonly TemplateTheme AlertTheme = new("#F43F5E", "#3A1220", "#9F1239");
    private static readonly TemplateTheme GenericTheme = new("#818CF8", "#1E1B4B", "#3730A3");

    /// <summary>
    /// Media queries and client resets. Kept as a non interpolated constant so CSS braces stay literal.
    /// </summary>
    private const string ResponsiveStyles = @"
        :root { color-scheme: dark light; supported-color-schemes: dark light; }
        body, table, td, a { -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%; }
        table, td { mso-table-lspace: 0pt; mso-table-rspace: 0pt; }
        img { -ms-interpolation-mode: bicubic; border: 0; outline: none; text-decoration: none; }
        a { text-decoration: none; }
        @media only screen and (max-width: 620px) {
            .nd-card { width: 100% !important; border-radius: 0 !important; }
            .nd-pad { padding-left: 22px !important; padding-right: 22px !important; }
            .nd-title { font-size: 21px !important; line-height: 30px !important; }
            .nd-wordmark { font-size: 23px !important; }
            .nd-stack { display: block !important; width: 100% !important; text-align: left !important; }
            .nd-cta { display: block !important; }
        }
    ";

    /// <summary>
    /// Produces the complete HTML document for a notification event.
    /// </summary>
    public string GenerateHtmlTemplate(NotificationEvent notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var theme = ResolveTheme(notification.Type);

        var (content, preheader) = notification.Type switch
        {
            NotificationType.ContactFormSubmission =>
                (RenderContactFormContent(notification, theme), BuildPreheader(notification.SenderName, notification.Subject)),
            NotificationType.WelcomeMessage =>
                (RenderWelcomeContent(notification, theme), BuildPreheader("Bienvenida", notification.Subject)),
            NotificationType.SystemAlert =>
                (RenderSystemAlertContent(notification, theme), BuildPreheader("Alerta de sistema", notification.Subject)),
            _ =>
                (RenderGenericContent(notification, theme), BuildPreheader("Evento de plataforma", notification.Subject))
        };

        return WrapInLayout(notification, theme, content, preheader);
    }

    /// <summary>
    /// Produces the plain text alternative. Multipart e-mails score noticeably better on spam
    /// filters and stay readable on clients that refuse to render HTML.
    /// </summary>
    public string GeneratePlainTextTemplate(NotificationEvent notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var builder = new StringBuilder();
        builder.AppendLine("NAINDEV - CLOUD NOTIFICATION SERVICE");
        builder.AppendLine("====================================");
        builder.AppendLine();
        builder.AppendLine($"Type      : {notification.Type}");
        builder.AppendLine($"Priority  : {notification.Priority}");
        builder.AppendLine($"Subject   : {Truncate(notification.Subject, MaxSubjectLength)}");

        if (!string.IsNullOrWhiteSpace(notification.SenderName))
        {
            builder.AppendLine($"Sender    : {notification.SenderName} <{notification.SenderEmail}>");
        }

        builder.AppendLine($"Recipient : {notification.Recipient}");
        builder.AppendLine($"Tracking  : {notification.NotificationId}");
        builder.AppendLine($"Timestamp : {notification.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        builder.AppendLine();
        builder.AppendLine("------------------------------------");
        builder.AppendLine();
        builder.AppendLine(notification.Body);
        builder.AppendLine();

        if (notification.Metadata.Count > 0)
        {
            builder.AppendLine("------------------------------------");
            builder.AppendLine("EVENT METADATA");
            foreach (var entry in notification.Metadata)
            {
                builder.AppendLine($"  {entry.Key}: {entry.Value}");
            }
            builder.AppendLine();
        }

        builder.AppendLine("------------------------------------");
        builder.AppendLine("Procesado de forma asincrona mediante RabbitMQ y MassTransit sobre .NET 10.");
        builder.AppendLine("https://www.naindev.com");

        return builder.ToString();
    }

    private static TemplateTheme ResolveTheme(NotificationType type) => type switch
    {
        NotificationType.ContactFormSubmission => ContactTheme,
        NotificationType.WelcomeMessage => WelcomeTheme,
        NotificationType.SystemAlert => AlertTheme,
        _ => GenericTheme
    };

    private static string WrapInLayout(NotificationEvent notification, TemplateTheme theme, string bodyContent, string preheader)
    {
        var documentTitle = Encode(Truncate(notification.Subject, MaxSubjectLength));
        var typeLabel = Encode(SplitPascalCase(notification.Type.ToString()));
        var priority = Encode(notification.Priority);
        var trackingId = Encode(notification.NotificationId.ToString());

        return $@"<!DOCTYPE html>
<html lang=""es"" xmlns:v=""urn:schemas-microsoft-com:vml"" xmlns:o=""urn:schemas-microsoft-com:office:office"">
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"" />
    <meta name=""color-scheme"" content=""dark light"" />
    <meta name=""supported-color-schemes"" content=""dark light"" />
    <title>{documentTitle}</title>
    <!--[if mso]>
    <noscript><xml><o:OfficeDocumentSettings><o:PixelsPerInch>96</o:PixelsPerInch></o:OfficeDocumentSettings></xml></noscript>
    <![endif]-->
    <style type=""text/css"">{ResponsiveStyles}</style>
</head>
<body style=""margin:0; padding:0; width:100%; background-color:#05070D; font-family:'Segoe UI',-apple-system,BlinkMacSystemFont,Roboto,Helvetica,Arial,sans-serif; color:#E8EEF9;"">
    <div style=""display:none; max-height:0; overflow:hidden; opacity:0; mso-hide:all;"">{Encode(preheader)}</div>

    <table role=""presentation"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""background-color:#05070D;"">
        <tr>
            <td align=""center"" style=""padding:36px 12px;"">

                <table role=""presentation"" class=""nd-card"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""600"" style=""width:600px; max-width:600px; background-color:#0F1523; border-radius:16px; border:1px solid #1E2A3F;"">

                    <tr>
                        <td style=""height:4px; line-height:4px; font-size:0; background-color:{theme.Accent}; border-radius:16px 16px 0 0;"">&nbsp;</td>
                    </tr>

                    <tr>
                        <td class=""nd-pad"" align=""center"" style=""padding:32px 36px 26px 36px; background-color:#0A0F1C;"">
                            <div class=""nd-wordmark"" style=""font-size:26px; font-weight:800; letter-spacing:2px; color:#FFFFFF; line-height:1;"">
                                NAIN<span style=""color:{theme.Accent};"">DEV</span>
                            </div>
                            <div style=""margin-top:9px; font-size:11px; letter-spacing:2.4px; text-transform:uppercase; color:#64748B; font-weight:600;"">
                                Cloud Notification Service
                            </div>
                        </td>
                    </tr>

                    <tr>
                        <td class=""nd-pad"" style=""padding:14px 36px; background-color:#0A0F1C; border-bottom:1px solid #1E2A3F;"">
                            <table role=""presentation"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"">
                                <tr>
                                    <td class=""nd-stack"" align=""left"" style=""font-size:11px;"">
                                        <span style=""display:inline-block; background-color:{theme.AccentSoft}; color:{theme.Accent}; padding:5px 12px; border-radius:999px; font-weight:700; letter-spacing:1.1px; text-transform:uppercase; border:1px solid {theme.AccentDeep};"">{typeLabel}</span>
                                    </td>
                                    <td class=""nd-stack"" align=""right"" style=""font-size:11px; color:#64748B; letter-spacing:0.8px; text-transform:uppercase; font-weight:600;"">
                                        Priority <span style=""color:#CBD5E1;"">{priority}</span>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <tr>
                        <td class=""nd-pad"" style=""padding:34px 36px 30px 36px; color:#CBD5E1; font-size:15px; line-height:24px;"">
                            {bodyContent}
                        </td>
                    </tr>

                    <tr>
                        <td class=""nd-pad"" style=""padding:22px 36px 26px 36px; background-color:#0A0F1C; border-top:1px solid #1E2A3F; border-radius:0 0 16px 16px;"">
                            <p style=""margin:0 0 10px 0; font-size:12px; line-height:19px; color:#64748B;"">
                                Notificación automática procesada de forma asíncrona mediante <span style=""color:#94A3B8;"">RabbitMQ</span> y <span style=""color:#94A3B8;"">MassTransit</span> sobre .NET 10.
                            </p>
                            <p style=""margin:0; font-size:12px; line-height:19px; color:#475569;"">
                                <a href=""https://www.naindev.com"" style=""color:{theme.Accent}; font-weight:600;"">www.naindev.com</a>
                                <span style=""color:#1E2A3F;"">&nbsp;|&nbsp;</span> Clean Architecture Engine
                                <span style=""color:#1E2A3F;"">&nbsp;|&nbsp;</span> &copy; {DateTime.UtcNow.Year} NainDev
                            </p>
                        </td>
                    </tr>

                </table>

                <div style=""max-width:600px; margin:18px auto 0 auto; font-size:11px; line-height:17px; color:#334155; text-align:center;"">
                    Tracking ID {trackingId}
                </div>

            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private static string RenderContactFormContent(NotificationEvent ev, TemplateTheme theme)
    {
        var senderName = Encode(Fallback(ev.SenderName, "Visitante anónimo"));
        var senderEmail = Encode(Fallback(ev.SenderEmail, "sin-correo@naindev.com"));
        var replySubject = Uri.EscapeDataString("RE: " + Truncate(ev.Subject, MaxSubjectLength));

        var rows = new List<KeyValuePair<string, string>>
        {
            new("Remitente", senderName),
            new("Correo electrónico", $@"<a href=""mailto:{senderEmail}"" style=""color:{theme.Accent}; font-weight:600;"">{senderEmail}</a>"),
            new("Asunto", Encode(Truncate(ev.Subject, MaxSubjectLength)))
        };

        return $@"
<h1 class=""nd-title"" style=""margin:0 0 12px 0; font-size:23px; line-height:32px; font-weight:700; color:#FFFFFF;"">
    Nuevo mensaje desde el portafolio
</h1>
<p style=""margin:0 0 26px 0; font-size:15px; line-height:24px; color:#94A3B8;"">
    Un visitante ha enviado una consulta a través del formulario de contacto de naindev.com.
</p>

{RenderDataCard(rows)}

<div style=""margin:24px 0 0 0; background-color:#080D18; border:1px solid #1E2A3F; border-left:3px solid {theme.Accent}; border-radius:10px;"">
    <div style=""padding:20px 22px; font-size:15px; line-height:25px; color:#E8EEF9; white-space:pre-line;"">{Encode(ev.Body)}</div>
</div>

{RenderCallToAction($"mailto:{senderEmail}?subject={replySubject}", "Responder al remitente", theme)}
{RenderTraceFooter(ev)}";
    }

    private static string RenderWelcomeContent(NotificationEvent ev, TemplateTheme theme)
    {
        var name = Encode(Fallback(ev.SenderName, "Dev"));

        return $@"
<h1 class=""nd-title"" style=""margin:0 0 14px 0; font-size:24px; line-height:33px; font-weight:700; color:#FFFFFF;"">
    Bienvenido al ecosistema NainDev
</h1>
<p style=""margin:0 0 18px 0; font-size:16px; line-height:26px; color:#E8EEF9;"">
    Hola <span style=""color:{theme.Accent}; font-weight:600;"">{name}</span>,
</p>
<p style=""margin:0; font-size:15px; line-height:26px; color:#94A3B8; white-space:pre-line;"">{Encode(ev.Body)}</p>

{RenderCallToAction("https://www.naindev.com", "Explorar el portafolio", theme)}

<p style=""margin:26px 0 0 0; padding-top:22px; border-top:1px solid #16203F; font-size:13px; line-height:21px; color:#64748B;"">
    Si has recibido este correo como parte de la demostración técnica del microservicio de
    notificaciones (.NET 10 y RabbitMQ), la prueba ha finalizado correctamente.
</p>
{RenderTraceFooter(ev)}";
    }

    private static string RenderSystemAlertContent(NotificationEvent ev, TemplateTheme theme)
    {
        var rows = new List<KeyValuePair<string, string>>
        {
            new("Instante del broker", Encode(ev.Timestamp.ToString("u"))),
            new("Destinatario", Encode(ev.Recipient))
        };

        return $@"
<table role=""presentation"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""background-color:{theme.AccentSoft}; border:1px solid {theme.AccentDeep}; border-radius:10px; margin:0 0 24px 0;"">
    <tr>
        <td style=""padding:14px 20px;"">
            <div style=""font-size:11px; letter-spacing:1.6px; text-transform:uppercase; font-weight:700; color:{theme.Accent};"">
                Alerta de sistema
            </div>
            <div style=""margin-top:5px; font-size:13px; line-height:20px; color:#FDA4AF;"">
                Se ha detectado una condición operativa que requiere revisión.
            </div>
        </td>
    </tr>
</table>

<h1 class=""nd-title"" style=""margin:0 0 20px 0; font-size:21px; line-height:30px; font-weight:700; color:#FFFFFF;"">
    {Encode(Truncate(ev.Subject, MaxSubjectLength))}
</h1>

<div style=""background-color:#080D18; border:1px solid #1E2A3F; border-radius:10px;"">
    <div style=""padding:8px 16px; border-bottom:1px solid #16203F; font-size:10px; letter-spacing:1.4px; text-transform:uppercase; color:#475569; font-weight:700;"">
        Traza del evento
    </div>
    <div style=""padding:18px 20px; font-family:Consolas,'SFMono-Regular',Menlo,monospace; font-size:13px; line-height:22px; color:#A5B4CF; white-space:pre-wrap; word-break:break-word;"">{Encode(ev.Body)}</div>
</div>

{RenderDataCard(rows, topMargin: 24)}
{RenderTraceFooter(ev)}";
    }

    private static string RenderGenericContent(NotificationEvent ev, TemplateTheme theme)
    {
        return $@"
<h1 class=""nd-title"" style=""margin:0 0 10px 0; font-size:22px; line-height:31px; font-weight:700; color:#FFFFFF;"">
    {Encode(Truncate(ev.Subject, MaxSubjectLength))}
</h1>
<p style=""margin:0 0 24px 0; font-size:12px; letter-spacing:0.6px; color:#64748B;"">
    Evento <span style=""color:{theme.Accent}; font-weight:600;"">{Encode(SplitPascalCase(ev.Type.ToString()))}</span>
</p>

<div style=""background-color:#080D18; border:1px solid #1E2A3F; border-radius:10px;"">
    <div style=""padding:22px; font-size:15px; line-height:25px; color:#E8EEF9; white-space:pre-line;"">{Encode(ev.Body)}</div>
</div>

{(ev.Metadata.Count > 0 ? RenderMetadataTable(ev.Metadata) : string.Empty)}
{RenderTraceFooter(ev)}";
    }

    /// <summary>
    /// Renders a labelled key/value card. Values are expected to be already encoded markup.
    /// </summary>
    private static string RenderDataCard(IReadOnlyList<KeyValuePair<string, string>> rows, int topMargin = 0)
    {
        var builder = new StringBuilder();
        builder.Append($@"
<table role=""presentation"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""margin:{topMargin}px 0 0 0; background-color:#0A101E; border:1px solid #1E2A3F; border-radius:10px;"">");

        for (var i = 0; i < rows.Count; i++)
        {
            var separator = i < rows.Count - 1 ? "border-bottom:1px solid #16203F;" : string.Empty;

            builder.Append($@"
    <tr>
        <td style=""padding:14px 20px; {separator}"">
            <div style=""font-size:10px; letter-spacing:1.4px; text-transform:uppercase; color:#475569; font-weight:700;"">{Encode(rows[i].Key)}</div>
            <div style=""margin-top:5px; font-size:15px; line-height:22px; color:#F1F5F9; font-weight:600; word-break:break-word;"">{rows[i].Value}</div>
        </td>
    </tr>");
        }

        builder.Append(@"
</table>");
        return builder.ToString();
    }

    /// <summary>
    /// Bulletproof call to action button, including the VML fallback required by Outlook.
    /// </summary>
    private static string RenderCallToAction(string href, string label, TemplateTheme theme)
    {
        var safeHref = Encode(href);
        var safeLabel = Encode(label);

        return $@"
<table role=""presentation"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""margin:28px 0 0 0;"">
    <tr>
        <td align=""center"">
            <!--[if mso]>
            <v:roundrect xmlns:v=""urn:schemas-microsoft-com:vml"" xmlns:w=""urn:schemas-microsoft-com:office:word"" href=""{safeHref}"" style=""height:46px;v-text-anchor:middle;width:260px;"" arcsize=""20%"" stroke=""f"" fillcolor=""{theme.Accent}"">
                <w:anchorlock/>
                <center style=""color:#05070D;font-family:sans-serif;font-size:15px;font-weight:bold;"">{safeLabel}</center>
            </v:roundrect>
            <![endif]-->
            <!--[if !mso]><!-- -->
            <a class=""nd-cta"" href=""{safeHref}"" style=""display:inline-block; background-color:{theme.Accent}; color:#05070D; font-size:15px; font-weight:700; line-height:46px; padding:0 34px; border-radius:9px; text-align:center; letter-spacing:0.3px;"">{safeLabel}</a>
            <!--<![endif]-->
        </td>
    </tr>
</table>";
    }

    private static string RenderMetadataTable(Dictionary<string, string> metadata)
    {
        var builder = new StringBuilder();
        builder.Append(@"
<div style=""margin-top:26px;"">
    <div style=""font-size:10px; letter-spacing:1.6px; text-transform:uppercase; color:#475569; font-weight:700; margin-bottom:10px;"">Metadatos del evento</div>
    <table role=""presentation"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""background-color:#080D18; border:1px solid #1E2A3F; border-radius:8px;"">");

        var index = 0;
        foreach (var entry in metadata)
        {
            var separator = index < metadata.Count - 1 ? "border-bottom:1px solid #16203F;" : string.Empty;
            builder.Append($@"
        <tr>
            <td width=""34%"" style=""padding:9px 16px; {separator} font-size:12px; color:#64748B; font-weight:600; vertical-align:top;"">{Encode(entry.Key)}</td>
            <td style=""padding:9px 16px; {separator} font-size:12px; color:#A5B4CF; font-family:Consolas,'SFMono-Regular',Menlo,monospace; word-break:break-all;"">{Encode(entry.Value)}</td>
        </tr>");
            index++;
        }

        builder.Append(@"
    </table>
</div>");
        return builder.ToString();
    }

    private static string RenderTraceFooter(NotificationEvent ev) => $@"
<p style=""margin:26px 0 0 0; font-size:11px; line-height:18px; color:#334155;"">
    Tracking <span style=""font-family:Consolas,'SFMono-Regular',Menlo,monospace; color:#475569;"">{Encode(ev.NotificationId.ToString())}</span>
    <span style=""color:#1E2A3F;"">&nbsp;|&nbsp;</span> {Encode(ev.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"))} UTC
</p>";

    private static string BuildPreheader(string lead, string subject) =>
        $"{Fallback(lead, "NainDev")} - {Truncate(subject, 90)}";

    /// <summary>
    /// Inserts a space before each inner capital letter so enum names read naturally.
    /// </summary>
    private static string SplitPascalCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length + 8);
        builder.Append(value[0]);

        for (var i = 1; i < value.Length; i++)
        {
            if (char.IsUpper(value[i]) && !char.IsUpper(value[i - 1]))
            {
                builder.Append(' ');
            }
            builder.Append(value[i]);
        }

        return builder.ToString();
    }

    private static string Fallback(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength
            ? value
            : string.Concat(value.AsSpan(0, maxLength - 1), "…");
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
