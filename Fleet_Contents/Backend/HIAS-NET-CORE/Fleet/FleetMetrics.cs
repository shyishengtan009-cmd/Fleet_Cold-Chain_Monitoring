using System;
using System.Linq;
using System.Threading;

namespace HIAS_NET_CORE.Fleet;

/// <summary>
/// Observable counters for the Fleet polling subsystem.
///
/// ── What is tracked ───────────────────────────────────────────────────────────
///   Poll cycles  — count + last duration + device count
///   Circuit breaker — how many devices are currently in open/backoff state
///   Email queue  — enqueue success / failure counts since startup
///   Cache        — hit / miss counts since startup
///
/// ── Thread safety ────────────────────────────────────────────────────────────
/// All integer counters use Interlocked.Increment/Read (lock-free atomic ops).
/// Volatile fields are used for single 64-bit reads/writes that need visibility.
///
/// ── Usage ─────────────────────────────────────────────────────────────────────
///   FleetMetrics.RecordPollCycle(durationMs, deviceCount);  // from FleetIngestService
///   FleetMetrics.RecordEmailEnqueued();                      // from FleetEmailQueue
///   FleetMetrics.RecordCacheHit();                           // from FleetCacheService
///   var snap = FleetMetrics.Snapshot();                      // for health endpoint
/// </summary>
public static class FleetMetrics
{
    private static long _pollCycles;
    private static long _emailsEnqueued;
    private static long _emailsFailed;
    private static long _cacheHits;
    private static long _cacheMisses;

    // volatile int is valid; long requires Interlocked.Read/Exchange for visibility
    private static volatile int  _lastPollDeviceCount;
    private static          long _lastPollDurationMs;
    private static          long _lastPollEpochMs;

    // ─── Record methods ───────────────────────────────────────────────────────

    public static void RecordPollCycle(long durationMs, int deviceCount)
    {
        Interlocked.Increment(ref _pollCycles);
        Interlocked.Exchange(ref _lastPollDurationMs, durationMs);
        _lastPollDeviceCount = deviceCount;
        Interlocked.Exchange(ref _lastPollEpochMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    public static void RecordEmailEnqueued() => Interlocked.Increment(ref _emailsEnqueued);
    public static void RecordEmailFailed()   => Interlocked.Increment(ref _emailsFailed);
    public static void RecordCacheHit()      => Interlocked.Increment(ref _cacheHits);
    public static void RecordCacheMiss()     => Interlocked.Increment(ref _cacheMisses);

    // ─── Snapshot ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns an immutable snapshot of all current metrics for the health endpoint.
    /// </summary>
    public static FleetMetricsSnapshot Snapshot()
    {
        var allStates      = FleetPollCircuitBreaker.AllStates;
        var openCircuits   = allStates.Count(kvp =>
            kvp.Value.State != FleetPollCircuitBreaker.CircuitState.Closed);

        long lastEpoch     = Interlocked.Read(ref _lastPollEpochMs);
        int  secsSincePoll = lastEpoch > 0
            ? (int)((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastEpoch) / 1000)
            : -1;

        return new FleetMetricsSnapshot(
            PollCycles:          Interlocked.Read(ref _pollCycles),
            LastPollDurationMs:  Interlocked.Read(ref _lastPollDurationMs),
            LastPollDeviceCount: _lastPollDeviceCount,
            SecsSinceLastPoll:   secsSincePoll,
            OpenCircuitBreakers: openCircuits,
            TotalDevicesTracked: allStates.Count,
            EmailsEnqueued:      Interlocked.Read(ref _emailsEnqueued),
            EmailsFailed:        Interlocked.Read(ref _emailsFailed),
            CacheHits:           Interlocked.Read(ref _cacheHits),
            CacheMisses:         Interlocked.Read(ref _cacheMisses)
        );
    }
}

/// <summary>
/// Immutable snapshot of Fleet metrics at a point in time.
/// Returned by FleetMetrics.Snapshot() and serialised by the health endpoint.
/// </summary>
public sealed record FleetMetricsSnapshot(
    long PollCycles,
    long LastPollDurationMs,
    int  LastPollDeviceCount,
    int  SecsSinceLastPoll,
    int  OpenCircuitBreakers,
    int  TotalDevicesTracked,
    long EmailsEnqueued,
    long EmailsFailed,
    long CacheHits,
    long CacheMisses
);
