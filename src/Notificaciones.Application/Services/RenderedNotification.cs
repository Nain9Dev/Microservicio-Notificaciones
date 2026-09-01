using System.Text;

namespace Notificaciones.Application.Services;

/// <summary>
/// Multipart payload produced by the template engine and handed over to the delivery provider.
/// Shipping both representations keeps the message readable on text-only clients and improves
/// the spam score assigned by most mail providers.
/// </summary>
public sealed record RenderedNotification(string Html, string PlainText)
{
    /// <summary>
    /// Size of the HTML representation in bytes, used for pipeline telemetry.
    /// </summary>
    public int HtmlBytes => Encoding.UTF8.GetByteCount(Html);
}
