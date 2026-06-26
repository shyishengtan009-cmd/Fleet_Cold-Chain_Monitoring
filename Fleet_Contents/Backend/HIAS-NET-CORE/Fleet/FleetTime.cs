using System;

namespace HIAS_NET_CORE.Fleet;

/// <summary>
/// Timezone helpers for Fleet alarm and schedule logic.
/// Replaces the hardcoded UTC+8 offset so the system works outside Malaysia.
///
/// Default timezone: "Asia/Kuala_Lumpur" (MYT, UTC+8).
/// Override per device via iot.tt19_devices.timezone column.
///
/// .NET 8 ships the IANA timezone database on all platforms, so IANA IDs
/// ("Asia/Kuala_Lumpur", "Asia/Singapore", etc.) work on both Windows and Linux.
/// </summary>
public static class FleetTime
{
    private static readonly TimeZoneInfo _default =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur");

    public static TimeZoneInfo Resolve(string? tzId)
    {
        if (string.IsNullOrWhiteSpace(tzId)) return _default;
        try   { return TimeZoneInfo.FindSystemTimeZoneById(tzId); }
        catch { return _default; }
    }

    public static DateTime UtcToLocal(DateTime utc, string? tzId) =>
        TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc), Resolve(tzId));
}
