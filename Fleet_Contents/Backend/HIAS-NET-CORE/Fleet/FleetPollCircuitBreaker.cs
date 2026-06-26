using System.Collections.Concurrent;

namespace HIAS_NET_CORE.Fleet;

/// <summary>
/// Per-device exponential backoff when TZone polls fail.
///
/// State machine per device:
///   Closed   — polling normally
///   Open     — backing off; polls are skipped until NextPollAllowed
///   HalfOpen — one probe allowed; success → Closed, failure → Open again
///
/// Backoff schedule (capped at 10 minutes):
///   1 fail  →  30 s
///   2 fails →  60 s
///   3 fails → 120 s
///   4 fails → 240 s
///   5+ fails→ 600 s
///
/// Thread-safe: all state is in a ConcurrentDictionary; individual DeviceState
/// fields are updated under the state's own lock.
/// </summary>
public static class FleetPollCircuitBreaker
{
    private const int  MaxBackoffSeconds = 600;
    private const int  BaseBackoffSeconds = 30;

    public enum CircuitState { Closed, Open, HalfOpen }

    public sealed class DeviceState
    {
        private readonly object _lock = new();

        public CircuitState State           { get; private set; } = CircuitState.Closed;
        public int          ConsecutiveFails { get; private set; }
        public DateTime     NextPollAllowed  { get; private set; } = DateTime.MinValue;
        public string?      LastError        { get; private set; }
        public DateTime     LastErrorAt      { get; private set; }
        public DateTime     LastSuccessAt    { get; private set; }

        public bool IsAllowed()
        {
            lock (_lock)
            {
                if (State == CircuitState.Closed)
                    return true;
                if (DateTime.UtcNow >= NextPollAllowed)
                {
                    State = CircuitState.HalfOpen;
                    return true;
                }
                return false;
            }
        }

        public void RecordSuccess()
        {
            lock (_lock)
            {
                State            = CircuitState.Closed;
                ConsecutiveFails = 0;
                LastSuccessAt    = DateTime.UtcNow;
                LastError        = null;
            }
        }

        public void RecordFailure(string error)
        {
            lock (_lock)
            {
                ConsecutiveFails++;
                LastError   = error;
                LastErrorAt = DateTime.UtcNow;

                var backoff = Math.Min(MaxBackoffSeconds,
                    BaseBackoffSeconds * (int)Math.Pow(2, ConsecutiveFails - 1));

                NextPollAllowed = DateTime.UtcNow.AddSeconds(backoff);
                State           = CircuitState.Open;
            }
        }
    }

    private static readonly ConcurrentDictionary<string, DeviceState> _states = new();

    public static DeviceState GetOrCreate(string hardwareId)
        => _states.GetOrAdd(hardwareId, _ => new DeviceState());

    /// <summary>
    /// Removes a device's circuit-breaker state entirely.
    /// Call when a device is unregistered so stale failure state is not inherited
    /// if the same hardware_id is re-registered later.
    /// </summary>
    public static void Remove(string hardwareId)
        => _states.TryRemove(hardwareId, out _);

    /// <summary>
    /// Returns a snapshot of all known device states (for the health endpoint).
    /// </summary>
    public static IReadOnlyDictionary<string, DeviceState> AllStates => _states;
}
