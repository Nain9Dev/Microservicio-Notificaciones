namespace Notificaciones.Application.Services;

/// <summary>
/// Masks personally identifiable values before they are broadcast to observability channels.
/// Telemetry from this platform is rendered on a publicly reachable dashboard, so raw
/// e-mail addresses must never leave the processing boundary.
/// </summary>
public static class PrivacyMasker
{
    private const int VisiblePrefixLength = 2;

    /// <summary>
    /// Converts an address into a partially redacted form, keeping just enough characters
    /// to correlate it visually with the original: "alberto@naindev.com" becomes "al*****@naindev.com".
    /// </summary>
    public static string MaskEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var trimmed = value.Trim();
        var atIndex = trimmed.IndexOf('@');

        if (atIndex <= 0)
        {
            return Redact(trimmed);
        }

        var localPart = trimmed[..atIndex];
        var domainPart = trimmed[atIndex..];

        return string.Concat(Redact(localPart), domainPart);
    }

    private static string Redact(string value)
    {
        if (value.Length <= VisiblePrefixLength)
        {
            return new string('*', value.Length);
        }

        var hiddenLength = Math.Min(value.Length - VisiblePrefixLength, 6);
        return string.Concat(value.AsSpan(0, VisiblePrefixLength), new string('*', hiddenLength));
    }
}
