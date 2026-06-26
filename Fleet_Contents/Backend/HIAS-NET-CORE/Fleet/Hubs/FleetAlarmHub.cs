using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HIAS_NET_CORE.Fleet.Hubs;

/// <summary>
/// SignalR hub for real-time Fleet alarm push notifications.
///
/// Clients subscribe to a per-device group ("fleet-alarm-{hardwareId}") and
/// receive "ReceiveAlarm" events whenever an alarm, warning, or dwell alert fires.
///
/// This replaces the frontend 15 s polling loop in useAlarmNotifier.ts with an
/// instant server-push that arrives within milliseconds of the alarm being evaluated.
///
/// Hub URL: /fleet-alarm-hub  (registered in Program.cs)
/// Push sender: FleetAlarmPusher (initialized in FleetIngestService constructor)
///
/// Auth: JWT bearer — token passed as ?access_token=... query parameter because
/// WebSocket connections cannot carry custom headers. Program.cs configures
/// JwtBearerEvents.OnMessageReceived to read the query parameter for this path.
/// </summary>
[Authorize]
public class FleetAlarmHub : Hub
{
    /// <summary>Joins the device-specific alarm group for this connection.</summary>
    public async Task Subscribe(string hardwareId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"fleet-alarm-{hardwareId}");

    /// <summary>Leaves the device-specific alarm group for this connection.</summary>
    public async Task Unsubscribe(string hardwareId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"fleet-alarm-{hardwareId}");
}
