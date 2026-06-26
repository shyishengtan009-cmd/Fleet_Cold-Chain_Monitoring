import { ref } from "vue";
import api from "@/helpers/api";
import { tripProgressState } from "./useTripProgressState";

export interface AlarmThresholds {
  temp_min_c: number | null;
  temp_max_c: number | null;
  humidity_min_pct: number | null;
  humidity_max_pct: number | null;
  light_min_lux: number | null;
  light_max_lux: number | null;
  vibration_g: number | null;
  battery_min_pct: number | null;
}

export const defaultThresholds: AlarmThresholds = {
  temp_min_c: null,
  temp_max_c: 30,
  humidity_min_pct: null,
  humidity_max_pct: 80,
  light_min_lux: null,
  light_max_lux: null,
  vibration_g: null,
  battery_min_pct: 20
};

function toNum(v: unknown): number | null {
  if (v === null || v === undefined || v === "") return null;
  const n = Number(v);
  return Number.isFinite(n) ? n : null;
}

function toStr(v: unknown): string {
  if (v === null || v === undefined) return "";
  return String(v);
}

export function useRtmSettings() {
  const thresholds = ref<AlarmThresholds>({ ...defaultThresholds });
  const trip = ref<any>({});
  const loading = ref(false);

  async function fetchSettings(hardwareId: string) {
    if (!hardwareId) return;
    loading.value = true;
    try {
      const result = await api.fleet.getDeviceSettings(hardwareId);
      const raw = result as unknown as Record<string, unknown>;
      const details = raw?.details as Record<string, unknown> | undefined;
      const data = raw?.data as Record<string, unknown> | undefined;
      const row = (details?.row ?? details ?? data?.row ?? data ?? raw?.row) as
        | Record<string, unknown>
        | undefined;
      if (!row || typeof row !== "object") return;

      const a = (row.alarm_json ?? row.alarmJson ?? {}) as Record<string, unknown>;
      thresholds.value = {
        temp_min_c: toNum(a.temp_min_c ?? a.tempMinC) ?? null,
        temp_max_c: toNum(a.temp_max_c ?? a.tempMaxC) ?? defaultThresholds.temp_max_c,
        humidity_min_pct: toNum(a.humidity_min_pct ?? a.humidityMinPct) ?? null,
        humidity_max_pct:
          toNum(a.humidity_max_pct ?? a.humidityMaxPct) ?? defaultThresholds.humidity_max_pct,
        light_min_lux: toNum(a.light_min_lux ?? a.lightMinLux) ?? null,
        light_max_lux: toNum(a.light_max_lux ?? a.lightMaxLux) ?? null,
        vibration_g: toNum(a.vibration_g ?? a.vibrationG) ?? null,
        battery_min_pct:
          toNum(a.battery_min_pct ?? a.batteryMinPct) ?? defaultThresholds.battery_min_pct
      };

      const t = (row.trip_json ?? row.tripJson ?? {}) as Record<string, unknown>;
      const tk = (k: string, sk: string) => toStr(t[k] ?? t[sk]);
      const tn = (k: string, sk: string) => toNum(t[k] ?? t[sk]);
      trip.value = {
        shipment_id: tk("shipment_id", "shipmentId"),
        goods: tk("goods", "goods"),
        carrier: tk("carrier", "carrier"),
        sender: tk("sender", "sender"),
        truck_name: tk("truck_name", "truckName"),
        estimated_time: tk("estimated_time", "estimatedTime"),
        remark: tk("remark", "remark"),
        customer_company: tk("customer_company", "customerCompany"),
        asset_id: tk("asset_id", "assetId"),
        transport_route: tk("transport_route", "transportRoute"),
        receiver: tk("receiver", "receiver"),
        start_location: tk("start_location", "startLocation"),
        end_location: tk("end_location", "endLocation"),
        geofence_required: !!(t.geofence_required ?? t.geofenceRequired),
        trip_min_minutes: tn("trip_min_minutes", "tripMinMinutes"),
        trip_max_minutes: tn("trip_max_minutes", "tripMaxMinutes"),
        notify_email: tk("notify_email", "notifyEmail")
      };
      // Sync tripProgressState from DB: if a trip is open (no end_time) set the real
      // start time so TripRouteIndicator tracks elapsed time accurately; reset otherwise.
      try {
        const tripsResult = await api.fleet.getTrips(hardwareId);
        const tripsData = ((tripsResult as any).details ?? tripsResult) as {
          trips: Record<string, unknown>[];
        };
        const tripsList: Record<string, unknown>[] = tripsData?.trips ?? [];
        const activeTrip = tripsList.find((t) => !t.end_time);
        if (activeTrip) {
          tripProgressState.value = {
            status: "Running",
            duration: "00:00:00",
            startTimeIso: activeTrip.start_time as string
          };
        } else if (tripProgressState.value.startTimeIso !== null) {
          // Only clear if it was previously set from a real trip — don't clobber
          // the fallback mount-time that TripRouteIndicator sets for itself.
          tripProgressState.value = { status: "Stopped", duration: "00:00:00", startTimeIso: null };
        }
      } catch {
        /* ignore */
      }
    } catch {
      /* silently keep defaults */
    } finally {
      loading.value = false;
    }
  }

  return { thresholds, trip, loading, fetchSettings };
}
