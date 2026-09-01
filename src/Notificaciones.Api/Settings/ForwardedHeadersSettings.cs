namespace Notificaciones.Api.Settings;

/// <summary>
/// Opt-in configuration for running the gateway behind a reverse proxy or load balancer.
///
/// Forwarded headers are only honoured when this section is explicitly enabled and the proxy
/// addresses are declared. Accepting them unconditionally would let any caller spoof its client
/// address and bypass every per-IP rate limit.
/// </summary>
public class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>
    /// Enables the forwarded headers middleware.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// IP addresses of the trusted proxies sitting in front of the gateway.
    /// </summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>
    /// Trusted networks expressed as CIDR ranges, for example "10.0.0.0/8".
    /// </summary>
    public string[] KnownNetworks { get; set; } = [];
}
