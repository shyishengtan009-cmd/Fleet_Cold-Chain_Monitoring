using System;
using Microsoft.Extensions.Logging;

namespace HIAS_NET_CORE.Fleet;

/// <summary>
/// Static logging facade for all Fleet (cold truck monitoring) components.
///
/// ── Why this exists ───────────────────────────────────────────────────────────
/// Most Fleet classes are static (they share a DB connection pool, not DI), so
/// they cannot receive an ILogger through constructor injection the way normal
/// classes do.  This wrapper bridges that gap:
///
///   1. FleetIngestService calls FleetLog.Initialize(logger) once at startup.
///   2. Every Fleet static class then calls FleetLog.Info / Warn / Error
///      instead of Console.WriteLine so all log output goes through Serilog
///      (structured logs, file sink, etc.).
///
/// ── Fallback ─────────────────────────────────────────────────────────────────
/// If Initialize() was never called (e.g. during unit tests or early startup),
/// messages fall back to Console.WriteLine so nothing is silently swallowed.
///
/// ── Usage ────────────────────────────────────────────────────────────────────
///   FleetLog.Info("[Fleet] Something happened");
///   FleetLog.Warn("[Fleet-Alarm] Threshold exceeded");
///   FleetLog.Error("[Fleet-DB] Query failed", ex);
/// </summary>
public static class FleetLog
{
    private static ILogger? _logger;

    // ─── Initialize ───────────────────────────────────────────────────────────

    /// <summary>
    /// Called once from FleetIngestService constructor after the DI logger is
    /// available. All subsequent Fleet log calls route through Serilog.
    /// </summary>
    public static void Initialize(ILogger logger) => _logger = logger;

    // ─── Log methods ──────────────────────────────────────────────────────────

    /// <summary>Logs an informational message (e.g. device polled, trip started).</summary>
    public static void Info(string message)
    {
        if (_logger != null)
            _logger.LogInformation("{Message}", message);
        else
            Console.WriteLine(message);
    }

    /// <summary>Logs a warning (e.g. missing config, suppressed alarm, duplicate GPS point).</summary>
    public static void Warn(string message)
    {
        if (_logger != null)
            _logger.LogWarning("{Message}", message);
        else
            Console.WriteLine("[WARN] " + message);
    }

    /// <summary>Logs a debug-level message (verbose per-poll diagnostics; only useful during dev/troubleshooting).</summary>
    public static void Debug(string message)
    {
        if (_logger != null)
            _logger.LogDebug("{Message}", message);
        // Intentionally silent on console fallback — debug output is too noisy for non-logger mode.
    }

    /// <summary>Logs an error with optional exception (e.g. DB write failed, email send failed).</summary>
    public static void Error(string message, Exception? ex = null)
    {
        if (_logger != null)
            _logger.LogError(ex, "{Message}", message);
        else
            Console.WriteLine("[ERROR] " + message + (ex != null ? $" — {ex.Message}" : ""));
    }
}
