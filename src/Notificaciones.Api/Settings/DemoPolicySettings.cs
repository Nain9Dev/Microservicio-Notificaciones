namespace Notificaciones.Api.Settings;

/// <summary>
/// Guard rails applied to the publicly reachable demo endpoint.
///
/// Without these rules the endpoint would accept an arbitrary recipient and, as soon as real SMTP
/// credentials are configured, turn the gateway into an open mail relay. The default configuration
/// redirects every demo dispatch to an internal sink address.
/// </summary>
public class DemoPolicySettings
{
    public const string SectionName = "DemoPolicy";

    /// <summary>
    /// When enabled, any recipient outside <see cref="AllowedRecipientDomains"/> is replaced by
    /// <see cref="SinkAddress"/> before the event reaches the broker.
    /// </summary>
    public bool RedirectUnknownRecipients { get; set; } = true;

    /// <summary>
    /// Internal address that absorbs sandboxed demo dispatches.
    /// </summary>
    public string SinkAddress { get; set; } = "demo-sink@naindev.com";

    /// <summary>
    /// Domains allowed to receive a real demo delivery, compared case-insensitively.
    /// </summary>
    public string[] AllowedRecipientDomains { get; set; } = [];

    /// <summary>
    /// Returns the effective recipient for a demo request together with a flag indicating whether
    /// the original value was replaced.
    /// </summary>
    public (string Recipient, bool WasRedirected) ResolveRecipient(string requestedRecipient)
    {
        if (!RedirectUnknownRecipients || string.IsNullOrWhiteSpace(requestedRecipient))
        {
            return (string.IsNullOrWhiteSpace(requestedRecipient) ? SinkAddress : requestedRecipient, false);
        }

        var atIndex = requestedRecipient.LastIndexOf('@');
        if (atIndex < 0 || atIndex == requestedRecipient.Length - 1)
        {
            return (SinkAddress, true);
        }

        var domain = requestedRecipient[(atIndex + 1)..];
        var isAllowed = AllowedRecipientDomains
            .Any(allowed => string.Equals(allowed, domain, StringComparison.OrdinalIgnoreCase));

        return isAllowed ? (requestedRecipient, false) : (SinkAddress, true);
    }
}
