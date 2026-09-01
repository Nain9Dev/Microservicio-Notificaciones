namespace Notificaciones.Api.Settings;

/// <summary>
/// Names of the rate limiting policies applied to the publicly reachable endpoints.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>Protects the real contact inbox from flooding.</summary>
    public const string ContactForm = "contact-form";

    /// <summary>Bounds how much broker traffic a single visitor can generate from the console.</summary>
    public const string DemoDispatch = "demo-dispatch";

    /// <summary>Preview is CPU bound and broker free, so it tolerates a much higher rate.</summary>
    public const string Preview = "template-preview";
}
