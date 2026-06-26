using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using HIAS_NET_CORE.Fleet.Hubs;

namespace HIAS_NET_CORE.Fleet;

/// <summary>
/// Static façade that pushes Fleet alarm events to connected SignalR clients.
///
/// Mirrors the FleetLog pattern: Initialize() is called once at startup
/// (from FleetIngestService constructor) to wire in the IHubContext.
/// All Fleet static classes (FleetAlarmChecker, FleetDwellChecker) then call
/// Push() without needing DI wiring.
///
/// Push() is fire-and-forget (caller uses "_ = FleetAlarmPusher.Push(...)").
/// Errors are logged as warnings but never propagate — a SignalR failure must
/// never stop the alarm evaluation or email path.
/// </summary>
public static class FleetAlarmPusher
{
    private static IHubContext<FleetAlarmHub>? _hub;

    /// <summary>Called once from FleetIngestService constructor.</summary>
    public static void Initialize(IHubContext<FleetAlarmHub> hub) => _hub = hub;

    /// <summary>
    /// Pushes one alarm payload to all clients subscribed to this device's group.
    /// The payload object is serialised as JSON by SignalR.
    /// No-op if Initialize() was never called (e.g. in tests or FleetSimService).
    /// </summary>
    public static async Task Push(string hardwareId, object payload)
    {
        if (_hub == null) return;
        try
        {
            await _hub.Clients
                      .Group($"fleet-alarm-{hardwareId}")
                      .SendAsync("ReceiveAlarm", payload);
        }
        catch (Exception ex)
        {
            FleetLog.Warn($"[Fleet-Push] Push failed for {hardwareId}: {ex.Message}");
        }
    }
}
