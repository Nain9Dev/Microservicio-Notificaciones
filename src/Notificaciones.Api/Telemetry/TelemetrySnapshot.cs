namespace Notificaciones.Api.Telemetry;

/// <summary>
/// Immutable read model exposed by the metrics endpoint and rendered by the live console.
/// </summary>
public sealed record TelemetrySnapshot
{
    /// <summary>Seconds elapsed since the gateway started.</summary>
    public long UptimeSeconds { get; init; }

    /// <summary>Notifications accepted by the HTTP gateway.</summary>
    public long Accepted { get; init; }

    /// <summary>Notifications successfully handed to a delivery provider.</summary>
    public long Dispatched { get; init; }

    /// <summary>Processing attempts that ended in failure.</summary>
    public long Failed { get; init; }

    /// <summary>Notifications accepted but not yet dispatched or failed.</summary>
    public int InFlight { get; init; }

    /// <summary>Percentage of processed notifications that reached the dispatch stage.</summary>
    public double SuccessRate { get; init; }

    /// <summary>Mean end-to-end worker latency in milliseconds.</summary>
    public double AverageLatencyMs { get; init; }

    /// <summary>95th percentile of the worker latency in milliseconds.</summary>
    public double P95LatencyMs { get; init; }

    /// <summary>Fastest observed worker latency in milliseconds.</summary>
    public double FastestLatencyMs { get; init; }

    /// <summary>Total bytes of HTML produced by the template engine.</summary>
    public long RenderedBytes { get; init; }

    /// <summary>Counters grouped by notification type.</summary>
    public IReadOnlyDictionary<string, long> ByType { get; init; } = new Dictionary<string, long>();

    /// <summary>Counters grouped by effective delivery channel.</summary>
    public IReadOnlyDictionary<string, long> ByChannel { get; init; } = new Dictionary<string, long>();

    /// <summary>Per-second dispatch counts for the last minute, oldest first.</summary>
    public IReadOnlyList<int> ThroughputSeries { get; init; } = Array.Empty<int>();

    /// <summary>Instant of the most recent lifecycle event, if any.</summary>
    public DateTimeOffset? LastEventAt { get; init; }
}
