using System;
using Microsoft.Extensions.Caching.Memory;

namespace FleetCore.Fleet;

/// <summary>
/// Abstraction layer for Fleet in-process caching.
///
/// ── Why this exists ───────────────────────────────────────────────────────────
/// FleetDbSettingsRepository and FleetDbLocationsRepository previously held their
/// own separate static MemoryCache instances. This class replaces both with a
/// single shared cache that can be swapped for a Redis-backed implementation when
/// Fleet scales to multiple app instances.
///
/// ── Redis readiness ───────────────────────────────────────────────────────────
/// At startup, before any cache operations run:
///   FleetCache.UseImplementation(new MyRedisFleetCache(connectionString));
/// Both repositories will use the new implementation immediately — no other
/// changes needed. The in-memory implementation is the default.
///
/// ── Cache key namespacing ─────────────────────────────────────────────────────
///   fleet:settings:{hardware_id}   — FleetDbSettingsRepository
///   fleet:all_locations            — FleetDbLocationsRepository
/// </summary>
public interface IFleetCache
{
    bool TryGet<T>(string key, out T? value);
    void Set<T>(string key, T value, TimeSpan ttl);
    void Remove(string key);
}

/// <summary>
/// In-process MemoryCache implementation of IFleetCache.
/// Records cache hits and misses to FleetMetrics for observability.
/// Safe for single-instance deployments; does NOT propagate invalidations
/// across multiple app instances (use Redis for multi-instance scale-out).
/// </summary>
public sealed class FleetMemoryCache : IFleetCache
{
    private readonly IMemoryCache _inner = new MemoryCache(new MemoryCacheOptions());

    public bool TryGet<T>(string key, out T? value)
    {
        if (_inner.TryGetValue(key, out value))
        {
            FleetMetrics.RecordCacheHit();
            return true;
        }
        value = default;
        FleetMetrics.RecordCacheMiss();
        return false;
    }

    public void Set<T>(string key, T value, TimeSpan ttl)
        => _inner.Set(key, value, ttl);

    public void Remove(string key)
        => _inner.Remove(key);
}

/// <summary>
/// Static accessor for the active Fleet cache implementation.
///
/// Repositories call FleetCache.TryGet / Set / Remove rather than holding their own
/// MemoryCache — this makes it trivial to swap in Redis by calling UseImplementation()
/// once at startup before FleetIngestService begins polling.
/// </summary>
public static class FleetCache
{
    private static volatile IFleetCache _current = new FleetMemoryCache();

    /// <summary>The active cache implementation (in-memory by default).</summary>
    public static IFleetCache Current => _current;

    /// <summary>
    /// Replaces the active cache implementation. Not thread-safe — call once at
    /// startup before any cache operations begin (e.g. in Program.cs or FleetIngestService ctor).
    /// </summary>
    public static void UseImplementation(IFleetCache impl)
        => _current = impl ?? throw new ArgumentNullException(nameof(impl));

    // Convenience forwarding methods so callers don't need to write FleetCache.Current.X(...)
    public static bool TryGet<T>(string key, out T? value) => _current.TryGet(key, out value);
    public static void Set<T>(string key, T value, TimeSpan ttl) => _current.Set(key, value, ttl);
    public static void Remove(string key) => _current.Remove(key);
}
