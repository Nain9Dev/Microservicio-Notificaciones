namespace Notificaciones.Application.Services;

/// <summary>
/// Outcome reported by a delivery provider once the notification leaves the processing boundary.
/// Exposing the effective channel keeps the observability layer honest: a simulated dispatch is
/// never presented as a real delivery.
/// </summary>
/// <param name="Channel">Effective delivery channel (for example "smtp" or "simulation").</param>
/// <param name="Detail">Short human readable description of what happened.</param>
/// <param name="Simulated">True when no real message left the process.</param>
public sealed record NotificationDeliveryResult(string Channel, string Detail, bool Simulated)
{
    public static NotificationDeliveryResult Smtp(string host, int port) =>
        new("smtp", $"Entregado al relay SMTP {host}:{port}", Simulated: false);

    public static NotificationDeliveryResult Simulation(string reason) =>
        new("simulation", reason, Simulated: true);
}
