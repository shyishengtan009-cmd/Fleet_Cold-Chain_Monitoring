import { ref } from "vue";
import api from "@/helpers/api";

export interface TripSummary {
  tripId: number;
  hardwareId: string;
  startTime: string;
  endTime: string | null;
  totalDistanceKm: number;
  pointsCount: number;
}

export interface LatLng {
  lat: number;
  lng: number;
  ts?: string;
}

export interface TripPoint {
  lat: number;
  lng: number;
  ts?: string;
  temp?: number | null;
}

export function extractLatLng(raw: unknown): LatLng | null {
  if (!raw || typeof raw !== "object") return null;
  const r = raw as Record<string, unknown>;

  if (typeof r.latLng === "string" && r.latLng.includes(",")) {
    const parts = (r.latLng as string).split(",").map((s) => s.trim());
    const lat = Number(parts[0]);
    const lng = Number(parts[1]);
    if (isFinite(lat) && isFinite(lng) && lat >= -90 && lat <= 90) return { lat, lng };
  }

  for (const [a, b] of [
    ["lat", "lng"],
    ["latitude", "longitude"],
    ["lat", "lon"]
  ]) {
    const lat = Number(r[a]);
    const lng = Number(r[b]);
    if (isFinite(lat) && isFinite(lng) && lat >= -90 && lat <= 90) return { lat, lng };
  }

  return null;
}

export function useRtmMap() {
  const trips = ref<TripSummary[]>([]);
  const tripsLoading = ref(false);
  const tripsError = ref<string | null>(null);

  const tripPoints = ref<LatLng[]>([]);
  const tripPointsLoading = ref(false);
  const selectedTripId = ref<number | null>(null);

  const saveLoading = ref(false);
  const saveError = ref<string | null>(null);

  // ── List trips ────────────────────────────────────────────────────────────
  async function fetchTrips(hardwareId: string) {
    tripsLoading.value = true;
    tripsError.value = null;
    try {
      const result = await api.fleet.getTrips(hardwareId);
      const data = ((result as any).details ?? result) as { trips: Record<string, unknown>[] };
      trips.value = (data?.trips ?? []).map(
        (t): TripSummary => ({
          tripId: Number(t.trip_id),
          hardwareId: t.hardware_id as string,
          startTime: t.start_time as string,
          endTime: t.end_time as string | null,
          totalDistanceKm: Number(t.total_distance_km ?? 0),
          pointsCount: Number(t.points_count ?? 0)
        })
      );
    } catch (e: unknown) {
      tripsError.value = String(e);
    } finally {
      tripsLoading.value = false;
    }
  }

  // ── Get trip points ────────────────────────────────────────────────────────
  async function fetchTripPoints(tripId: number) {
    selectedTripId.value = tripId;
    tripPointsLoading.value = true;
    tripPoints.value = [];
    try {
      const result = await api.fleet.getTripById(tripId);
      const data = ((result as any).details ?? result) as Record<string, unknown>;

      // trip_data is stored as JSON string in the DB, parse it
      let tripData: Record<string, unknown> = {};
      const raw = data?.trip_data;
      if (typeof raw === "string") {
        try {
          tripData = JSON.parse(raw);
        } catch {
          tripData = {};
        }
      } else if (raw && typeof raw === "object") {
        tripData = raw as Record<string, unknown>;
      }

      const points = (tripData["points"] as TripPoint[] | undefined) ?? [];
      tripPoints.value = points
        .map((p) => {
          const ll = extractLatLng(p);
          if (!ll) return null;
          return { ...ll, ts: p.ts } as LatLng;
        })
        .filter((p): p is LatLng => p !== null);
    } catch {
      tripPoints.value = [];
    } finally {
      tripPointsLoading.value = false;
    }
  }

  // ── Save trip to PostgreSQL ────────────────────────────────────────────────
  async function saveTrip(
    hardwareId: string,
    startTime: string,
    endTime: string,
    totalDistanceKm: number,
    points: TripPoint[]
  ): Promise<TripSummary | null> {
    saveLoading.value = true;
    saveError.value = null;
    try {
      const resp = await api.fleet.saveTrip({
        hardware_id: hardwareId,
        start_time: startTime,
        end_time: endTime,
        total_distance_km: totalDistanceKm,
        points
      });
      const d = ((resp as any).details ?? resp) as Record<string, unknown>;
      return {
        tripId: Number(d.trip_id),
        hardwareId: d.hardware_id as string,
        startTime: d.start_time as string,
        endTime: (d.end_time as string | null) ?? null,
        totalDistanceKm: Number(d.total_distance_km ?? 0),
        pointsCount: Number(d.points_count ?? 0)
      };
    } catch (e: unknown) {
      saveError.value = String(e);
      return null;
    } finally {
      saveLoading.value = false;
    }
  }

  // ── Open trip (FIX 3: create open trip on Start) ──────────────────────────
  async function openTrip(hardwareId: string, startTime: string): Promise<number | null> {
    try {
      const resp = await api.fleet.openTrip(hardwareId, startTime);
      const d = ((resp as any).details ?? resp) as Record<string, unknown>;
      return Number(d.trip_id) || null;
    } catch {
      return null;
    }
  }

  // ── Close trip (FIX 3: close open trip on Stop, recalculates from DB points) ─
  async function closeTrip(tripId: number, endTime: string): Promise<TripSummary | null> {
    saveLoading.value = true;
    saveError.value = null;
    try {
      const resp = await api.fleet.closeTrip(tripId, endTime);
      const d = ((resp as any).details ?? resp) as Record<string, unknown>;
      return {
        tripId: Number(d.trip_id),
        hardwareId: d.hardware_id as string,
        startTime: d.start_time as string,
        endTime: d.end_time as string | null,
        totalDistanceKm: Number(d.total_distance_km ?? 0),
        pointsCount: Number(d.points_count ?? 0)
      };
    } catch (e: unknown) {
      saveError.value = String(e);
      return null;
    } finally {
      saveLoading.value = false;
    }
  }

  function clearTrip() {
    selectedTripId.value = null;
    tripPoints.value = [];
  }

  return {
    trips,
    tripsLoading,
    tripsError,
    tripPoints,
    tripPointsLoading,
    selectedTripId,
    saveLoading,
    saveError,
    fetchTrips,
    fetchTripPoints,
    saveTrip,
    openTrip,
    closeTrip,
    clearTrip
  };
}
