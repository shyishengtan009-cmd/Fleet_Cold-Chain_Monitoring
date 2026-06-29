using System;
using System.Collections.Generic;

namespace FleetCore.Fleet.Ingest;

/// <summary>
/// Represents one physical cold-truck sensor device that the ingest service polls.
///
/// ── What this file does ───────────────────────────────────────────────────────
///   FleetDevice           — record describing one device (IDs + API credentials)
///   FleetDeviceLoader     — loads the device list from environment variables
///
/// ── Two sources of device lists ───────────────────────────────────────────────
/// The ingest service discovers which devices to poll from TWO places (in priority order):
///
///   1. The PostgreSQL database (iot.tt19_devices) — the recommended source.
///      Devices registered via the Fleet Devices page are found here.
///      See FleetDbDevices.GetAllActiveDevices().
///
///   2. Environment variables — a fallback for quick development/staging setups
///      when you want to override or test without touching the database.
///      See LoadDevicesFromEnv() below.
///
/// FleetIngestService tries the database first. If the database returns no devices
/// (or if db discovery is disabled), it falls back to environment variables.
///
/// ── Environment variable formats ──────────────────────────────────────────────
///
///   Multi-device (recommended for production with multiple trucks):
///     FLEET_DEVICES = "deviceIntId1:hardwareId1, deviceIntId2:hardwareId2, ..."
///     Example: "12345:AABBCCDD, 67890:EEFFGGHH"
///
///   Single-device shorthand (for simple setups or testing):
///     FLEET_DEVICE_INT_ID = "12345"
///     FLEET_HARDWARE_ID   = "AABBCCDD"
///
/// ── What is device_int_id? ────────────────────────────────────────────────────
/// TZone uses two different identifiers for each device:
///   - hardware_id  (string, e.g. "AABBCCDD") — physical device serial number,
///                  used as the primary key in iot.tt19_devices
///   - device_int_id (integer) — TZone's internal numeric ID, required to call
///                  the TZone REST API (/Data/Realtime/{id}, /Device/ID/{id})
///
/// To find a device's integer ID: call GET /Device?key={hardware_id} on the
/// TZone Swagger UI after authenticating. See the device onboarding guide in
/// memory/tt19-device-onboarding.md for the full step-by-step process.
/// </summary>
public sealed record FleetDevice(
    long    DeviceIntId,
    string  HardwareId,
    string? AppId     = null,
    string? AppKey    = null,
    string? AppSecret = null,
    string  Timezone  = "Asia/Kuala_Lumpur",
    string? Label     = null);

/// <summary>
/// Loads FleetDevice records from environment variables.
/// Used by FleetIngestService as the fallback device source when the database
/// returns no devices.
/// </summary>
public static class FleetDeviceLoader
{
    // ─── LoadDevicesFromEnv ───────────────────────────────────────────────────

    /// <summary>
    /// Reads the FLEET_DEVICES environment variable (multi-device) or the
    /// FLEET_DEVICE_INT_ID + FLEET_HARDWARE_ID pair (single-device).
    ///
    /// Throws InvalidOperationException with a clear message if the env vars
    /// are present but malformed — this is intentional so a misconfigured
    /// deployment fails loudly at startup rather than silently polling nothing.
    ///
    /// If neither FLEET_DEVICES nor FLEET_DEVICE_INT_ID is set, this method
    /// throws. The caller (FleetIngestService) only calls this as a fallback
    /// when the DB returns zero devices, so a throw here stops the service.
    /// </summary>
    public static List<FleetDevice> LoadDevicesFromEnv()
    {
        // ── Multi-device format: FLEET_DEVICES = "intId1:hwId1, intId2:hwId2" ──
        var multi = (Environment.GetEnvironmentVariable("FLEET_DEVICES") ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(multi))
        {
            var list = new List<FleetDevice>();
            foreach (var part in multi.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var bits = part.Split(':', 2, StringSplitOptions.TrimEntries);
                if (bits.Length != 2)
                    throw new InvalidOperationException(
                        $"Bad FLEET_DEVICES format for entry '{part}'. Expected: deviceIntId:hardwareId");

                if (!long.TryParse(bits[0], out var deviceId))
                    throw new InvalidOperationException(
                        $"FLEET_DEVICES: cannot parse '{bits[0]}' as a device integer ID.");

                var hw = bits[1].Trim();
                if (string.IsNullOrWhiteSpace(hw))
                    throw new InvalidOperationException(
                        $"FLEET_DEVICES: hardware_id is empty for device integer ID {deviceId}.");

                list.Add(new FleetDevice(deviceId, hw));
            }
            return list;
        }

        // ── Single-device fallback: FLEET_DEVICE_INT_ID + FLEET_HARDWARE_ID ──
        var idStr = (Environment.GetEnvironmentVariable("FLEET_DEVICE_INT_ID") ?? "").Trim();
        var hwId  = (Environment.GetEnvironmentVariable("FLEET_HARDWARE_ID")   ?? "").Trim();

        if (string.IsNullOrWhiteSpace(idStr))
            throw new InvalidOperationException(
                "No devices found in the database and FLEET_DEVICE_INT_ID is not set. " +
                "Set FLEET_DEVICES or FLEET_DEVICE_INT_ID, or register a device via the Fleet Devices page.");

        if (!long.TryParse(idStr, out var id))
            throw new InvalidOperationException($"FLEET_DEVICE_INT_ID must be a number, got '{idStr}'.");

        if (string.IsNullOrWhiteSpace(hwId))
            throw new InvalidOperationException("FLEET_HARDWARE_ID is missing.");

        return new List<FleetDevice> { new FleetDevice(id, hwId) };
    }
}
