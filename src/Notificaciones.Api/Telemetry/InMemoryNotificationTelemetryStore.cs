using Notificaciones.Domain.Events;

namespace Notificaciones.Api.Telemetry;

/// <summary>
/// Bounded, lock-protected projection of the notification pipeline.
///
/// The store is deliberately volatile: it powers a live operations console, not an audit log.
/// Every buffer has a hard ceiling so a burst of traffic can never grow the process memory,
/// and restarting the gateway resets the counters by design.
/// </summary>
public sealed class InMemoryNotificationTelemetryStore : INotificationTelemetryStore
{
    private const int MaxRecentEvents = 240;
    private const int MaxLatencySamples = 512;
    private const int MaxInFlight = 2_000;
    private const int ThroughputWindowSeconds = 60;

    private readonly Lock _gate = new();
    private readonly Queue<NotificationLifecycleEvent> _recentEvents = new(MaxRecentEvents);
    private readonly HashSet<Guid> _inFlight = [];
    private readonly Dictionary<string, long> _byType = [];
    private readonly Dictionary<string, long> _byChannel = [];

    private readonly double[] _latencySamples = new double[MaxLatencySamples];
    private int _latencyCount;
    private int _latencyCursor;

    private readonly int[] _throughputBuckets = new int[ThroughputWindowSeconds];
    private readonly long[] _throughputStamps = new long[ThroughputWindowSeconds];

    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private readonly TimeProvider _timeProvider;

    private long _accepted;
    private long _dispatched;
    private long _failed;
    private long _renderedBytes;
    private double _fastestLatencyMs = double.MaxValue;
    private DateTimeOffset? _lastEventAt;

    public InMemoryNotificationTelemetryStore(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public void Record(NotificationLifecycleEvent lifecycleEvent)
    {
        ArgumentNullException.ThrowIfNull(lifecycleEvent);

        lock (_gate)
        {
            AppendToRecentBuffer(lifecycleEvent);
            _lastEventAt = lifecycleEvent.Timestamp;

            switch (lifecycleEvent.Stage)
            {
                case NotificationStage.Accepted:
                    _accepted++;
                    Increment(_byType, lifecycleEvent.Type.ToString());
                    TrackInFlight(lifecycleEvent.NotificationId);
                    break;

                case NotificationStage.Rendered:
                    _renderedBytes += lifecycleEvent.PayloadBytes;
                    break;

                case NotificationStage.Dispatched:
                    _dispatched++;
                    _inFlight.Remove(lifecycleEvent.NotificationId);
                    RecordLatency(lifecycleEvent.ElapsedMilliseconds);
                    RecordThroughput();

                    if (!string.IsNullOrWhiteSpace(lifecycleEvent.Channel))
                    {
                        Increment(_byChannel, lifecycleEvent.Channel);
                    }
                    break;

                case NotificationStage.Failed:
                    _failed++;
                    _inFlight.Remove(lifecycleEvent.NotificationId);
                    break;
            }
        }
    }

    public IReadOnlyList<NotificationLifecycleEvent> GetRecentEvents(int limit)
    {
        if (limit <= 0)
        {
            return [];
        }

        lock (_gate)
        {
            // The queue keeps insertion order, so take from the tail and reverse to get newest first.
            return _recentEvents
                .Skip(Math.Max(0, _recentEvents.Count - limit))
                .Reverse()
                .ToArray();
        }
    }

    public TelemetrySnapshot GetSnapshot()
    {
        lock (_gate)
        {
            var processed = _dispatched + _failed;

            return new TelemetrySnapshot
            {
                UptimeSeconds = (long)(_timeProvider.GetUtcNow() - _startedAt).TotalSeconds,
                Accepted = _accepted,
                Dispatched = _dispatched,
                Failed = _failed,
                InFlight = _inFlight.Count,
                SuccessRate = processed == 0 ? 100d : Math.Round(_dispatched * 100d / processed, 2),
                AverageLatencyMs = CalculateAverageLatency(),
                P95LatencyMs = CalculatePercentileLatency(0.95),
                FastestLatencyMs = _latencyCount == 0 ? 0 : Math.Round(_fastestLatencyMs, 2),
                RenderedBytes = _renderedBytes,
                ByType = new Dictionary<string, long>(_byType),
                ByChannel = new Dictionary<string, long>(_byChannel),
                ThroughputSeries = BuildThroughputSeries(),
                LastEventAt = _lastEventAt
            };
        }
    }

    private void AppendToRecentBuffer(NotificationLifecycleEvent lifecycleEvent)
    {
        _recentEvents.Enqueue(lifecycleEvent);

        while (_recentEvents.Count > MaxRecentEvents)
        {
            _recentEvents.Dequeue();
        }
    }

    /// <summary>
    /// Adds an identifier to the in-flight set, discarding the whole set if it ever grows past
    /// its ceiling. Losing the gauge is preferable to leaking identifiers of messages whose
    /// terminal stage never arrived (broker down, worker killed mid-flight).
    /// </summary>
    private void TrackInFlight(Guid notificationId)
    {
        if (_inFlight.Count >= MaxInFlight)
        {
            _inFlight.Clear();
        }

        _inFlight.Add(notificationId);
    }

    private void RecordLatency(double elapsedMilliseconds)
    {
        if (elapsedMilliseconds <= 0)
        {
            return;
        }

        _latencySamples[_latencyCursor] = elapsedMilliseconds;
        _latencyCursor = (_latencyCursor + 1) % MaxLatencySamples;

        if (_latencyCount < MaxLatencySamples)
        {
            _latencyCount++;
        }

        if (elapsedMilliseconds < _fastestLatencyMs)
        {
            _fastestLatencyMs = elapsedMilliseconds;
        }
    }

    private void RecordThroughput()
    {
        var currentSecond = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var bucket = (int)(currentSecond % ThroughputWindowSeconds);

        if (_throughputStamps[bucket] != currentSecond)
        {
            _throughputStamps[bucket] = currentSecond;
            _throughputBuckets[bucket] = 0;
        }

        _throughputBuckets[bucket]++;
    }

    private int[] BuildThroughputSeries()
    {
        var currentSecond = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var series = new int[ThroughputWindowSeconds];

        for (var offset = 0; offset < ThroughputWindowSeconds; offset++)
        {
            var second = currentSecond - (ThroughputWindowSeconds - 1 - offset);
            var bucket = (int)(((second % ThroughputWindowSeconds) + ThroughputWindowSeconds) % ThroughputWindowSeconds);

            series[offset] = _throughputStamps[bucket] == second ? _throughputBuckets[bucket] : 0;
        }

        return series;
    }

    private double CalculateAverageLatency()
    {
        if (_latencyCount == 0)
        {
            return 0;
        }

        var total = 0d;
        for (var i = 0; i < _latencyCount; i++)
        {
            total += _latencySamples[i];
        }

        return Math.Round(total / _latencyCount, 2);
    }

    private double CalculatePercentileLatency(double percentile)
    {
        if (_latencyCount == 0)
        {
            return 0;
        }

        var ordered = new double[_latencyCount];
        Array.Copy(_latencySamples, ordered, _latencyCount);
        Array.Sort(ordered);

        var index = (int)Math.Ceiling(percentile * ordered.Length) - 1;
        index = Math.Clamp(index, 0, ordered.Length - 1);

        return Math.Round(ordered[index], 2);
    }

    private static void Increment(Dictionary<string, long> counters, string key)
    {
        counters[key] = counters.TryGetValue(key, out var current) ? current + 1 : 1;
    }
}
