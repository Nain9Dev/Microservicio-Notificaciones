namespace Notificaciones.Api.Telemetry;

/// <summary>
/// Immutable description of how this gateway instance was wired at start-up.
/// Surfaced by the health endpoint so the console can state the truth about the running topology
/// instead of assuming a RabbitMQ cluster is always behind it.
/// </summary>
/// <param name="TransportMode">Effective transport: "rabbitmq" or "in-memory".</param>
/// <param name="RunsWorkerInProcess">True when the gateway also hosts the notification consumer.</param>
/// <param name="InstanceId">Short identifier of this process, used to name temporary broker queues.</param>
/// <param name="StartedAt">UTC instant the process started.</param>
public sealed record GatewayRuntimeInfo(
    string TransportMode,
    bool RunsWorkerInProcess,
    string InstanceId,
    DateTimeOffset StartedAt);
