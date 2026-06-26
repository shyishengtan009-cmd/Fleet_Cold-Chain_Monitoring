<script setup lang="ts">
import { ref, watch, onMounted, onUnmounted, nextTick } from "vue";
import L_lib from "leaflet";
import "leaflet/dist/leaflet.css";
import type { HistoryRow } from "./useRtmHistory";
import { useRtmMap, extractLatLng, type LatLng, type TripPoint } from "./useRtmMap";
import { fmtTs } from "./rtmFormat";
import { tripProgressState } from "./useTripProgressState";
import LocationsPanel, { type FleetLocation } from "./LocationsPanel.vue";

const props = defineProps<{
  hardwareId: string;
  historyRows: HistoryRow[];
  truckName: string | null;
}>();

// ── Composable ────────────────────────────────────────────────────────────
const {
  trips,
  tripsLoading,
  tripsError,
  tripPoints,
  saveLoading,
  saveError,
  fetchTrips,
  fetchTripPoints,
  saveTrip,
  openTrip,
  closeTrip
} = useRtmMap();

// ── Leaflet ───────────────────────────────────────────────────────────────
const mapEl = ref<HTMLDivElement | null>(null);
const leafletMap = ref<any>(null);
const L = ref<any>(null);

let liveMarker: any = null;
let livePolyline: any = null;
let tripPolyline: any = null;
let startMarker: any = null;
let endMarker: any = null;
let streetLayer: any = null;
let satelliteLayer: any = null;
const isSatellite = ref(false);
let breadcrumbs: any[] = [];
let breadcrumbPtIdx = 0; // tripTrail index up to which breadcrumbs have been placed
let breadcrumbKmAcc = 0; // km accumulated since the last placed breadcrumb
let breadcrumbTotalKm = 0; // cumulative km at the last placed breadcrumb

// ── Location Tracking ─────────────────────────────────────────────────────
const livePoints = ref<LatLng[]>([]);
const liveLatest = ref<LatLng | null>(null);
const liveTs = ref<string | null>(null);
const liveStatus = ref("Waiting for device…");

// ── Trip history lock — prevents live track from overwriting map view ──────
const lockTrack = ref(false);
const trackCleared = ref(false); // set by Clear Track button; suppresses rebuilds until device changes

// ── Trip Tracking (active session) ────────────────────────────────────────
type TripStatus = "Stopped" | "Running";
const tripStatus = ref<TripStatus>("Stopped");
const tripDistKm = ref(0);
const tripPtCount = ref(0);
const tripDuration = ref("00:00:00");
const avgTemp = ref<number | null>(null);
const saveSuccess = ref(false);
const tripRecovered = ref(false);

const activeTripId = ref<number | null>(null); // FIX 3: DB trip id for open trip
const serverPtCount = ref<number | null>(null); // points collected server-side (shown after restore)
const showStopConfirm = ref(false); // confirmation dialog state

// ── Named Locations ───────────────────────────────────────────────────────
const locationsPanelRef = ref<InstanceType<typeof LocationsPanel> | null>(null);
const showLocationsPanel = ref(false);
const pickingOnMap = ref(false);
const parkedSince = ref<Date | null>(null);
const knownLocations = ref<FleetLocation[]>([]);

let tripStartTime: Date | null = null;
let durationTimer: ReturnType<typeof setInterval> | null = null;
let autoDetectTimer: ReturnType<typeof setInterval> | null = null; // polls for scheduler-started trips (Stopped state)
let tripSyncTimer: ReturnType<typeof setInterval> | null = null; // polls to detect scheduler-closed trips (Running state)
let tripTrail: TripPoint[] = []; // points collected during active trip
let lastTripPoint: LatLng | null = null;
let tripBoundsFit = false; // fitBounds only once at trip start, not every poll
let wasStationary = false; // tracks stationarity across buildLiveTrack() calls for parked-duration
let locationCircles: any[] = []; // Leaflet circle layers for named stop locations

// ── localStorage persistence ───────────────────────────────────────────────
function tripStorageKey() {
  return `rtm_trip_${props.hardwareId}`;
}

function persistTrip() {
  if (tripStatus.value !== "Running") return;
  try {
    localStorage.setItem(
      tripStorageKey(),
      JSON.stringify({
        startTime: tripStartTime?.toISOString(),
        distKm: tripDistKm.value,
        ptCount: tripPtCount.value,
        trail: tripTrail,
        activeTripId: activeTripId.value,
        savedAt: new Date().toISOString()
      })
    );
  } catch (e) {
    // ignore storage errors
    void e;
  }
}

function clearPersistedTrip() {
  try {
    localStorage.removeItem(tripStorageKey());
  } catch (e) {
    void e;
  }
}

function restorePersistedTrip(): boolean {
  try {
    const raw = localStorage.getItem(tripStorageKey());
    if (!raw) return false;
    const saved = JSON.parse(raw);
    if (!saved?.startTime) return false;

    // Discard stale saves — a trip persisted more than 24 hours ago is no longer valid
    const savedAt = saved.savedAt ? new Date(saved.savedAt).getTime() : 0;
    if (Date.now() - savedAt > 24 * 3_600_000) {
      localStorage.removeItem(tripStorageKey());
      return false;
    }

    tripStartTime = new Date(saved.startTime);
    tripDistKm.value = saved.distKm ?? 0;
    tripPtCount.value = saved.ptCount ?? 0;
    tripTrail = saved.trail ?? [];
    lastTripPoint = tripTrail.length ? tripTrail[tripTrail.length - 1] : null;
    activeTripId.value = saved.activeTripId ?? null;
    tripStatus.value = "Running";
    saveSuccess.value = false;
    return true;
  } catch {
    return false;
  }
}

// ── localStorage GC — sweep orphaned rtm_trip_* keys from deleted devices ─
function sweepStaleStorageKeys() {
  const STALE_MS = 24 * 3_600_000;
  const now = Date.now();
  const toDelete: string[] = [];
  for (let i = 0; i < localStorage.length; i++) {
    const key = localStorage.key(i);
    if (!key?.startsWith("rtm_trip_")) continue;
    try {
      const parsed = JSON.parse(localStorage.getItem(key) ?? "{}");
      const savedAt = parsed.savedAt ? new Date(parsed.savedAt).getTime() : 0;
      if (now - savedAt > STALE_MS) toDelete.push(key);
    } catch {
      toDelete.push(key);
    }
  }
  toDelete.forEach((k) => localStorage.removeItem(k));
}

// ── Wake Lock — keeps screen/tab alive while map is mounted ───────────────
let _wakeLock: any = null;

async function acquireWakeLock() {
  if (!("wakeLock" in navigator)) return;
  try {
    _wakeLock = await (navigator as any).wakeLock.request("screen");
    _wakeLock.addEventListener("release", () => {
      _wakeLock = null;
    });
  } catch {
    /* browser denied or not supported */
  }
}

function releaseWakeLock() {
  try {
    _wakeLock?.release();
  } catch (e) {
    void e;
  }
  _wakeLock = null;
}

// Pause 1s duration ticker when tab is hidden; resume + re-acquire wake lock when visible
function onVisibilityChange() {
  if (document.visibilityState === "hidden") {
    if (durationTimer) {
      clearInterval(durationTimer);
      durationTimer = null;
    }
    return;
  }
  acquireWakeLock();
  if (tripStatus.value === "Running") startDurationTimer();
}

// ── Trip History ──────────────────────────────────────────────────────────
const selectedHistoryId = ref<number | null>(null);

function tripDurationLabel(start: string, end: string | null): string {
  if (!end) return "ongoing";
  const ms = new Date(end).getTime() - new Date(start).getTime();
  if (ms <= 0) return "—";
  const h = Math.floor(ms / 3600000);
  const m = Math.floor((ms % 3600000) / 60000);
  return h > 0 ? `${h}h ${m}m` : `${m}m`;
}

function backToLive() {
  lockTrack.value = false;
  selectedHistoryId.value = null;
  tripPolyline?.setLatLngs([]);
  if (startMarker) {
    leafletMap.value?.removeLayer(startMarker);
    startMarker = null;
  }
  if (endMarker) {
    leafletMap.value?.removeLayer(endMarker);
    endMarker = null;
  }
  buildLiveTrack();
}

function locateDevice() {
  if (!liveLatest.value || !leafletMap.value) return;
  leafletMap.value.setView([liveLatest.value.lat, liveLatest.value.lng], 15, { animate: true });
  if (liveMarker) liveMarker.openPopup();
}

// ── Leaflet init ──────────────────────────────────────────────────────────
async function initMap() {
  if (!mapEl.value) return;
  L.value = L_lib;
  leafletMap.value = L.value
    .map(mapEl.value, { zoomControl: true })
    .setView([3.1412, 101.6865], 12);

  streetLayer = L.value.tileLayer(
    "https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png",
    {
      maxZoom: 20,
      attribution:
        '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors © <a href="https://carto.com/">CARTO</a>'
    }
  );
  satelliteLayer = L.value.tileLayer(
    "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}",
    { maxZoom: 19, attribution: "© Esri World Imagery" }
  );
  streetLayer.addTo(leafletMap.value);

  livePolyline = L.value.polyline([], { color: "#2196F3", weight: 4 }).addTo(leafletMap.value);
  tripPolyline = L.value
    .polyline([], { color: "#9C27B0", weight: 4, dashArray: "8,4" })
    .addTo(leafletMap.value);
}

function toggleTileLayer() {
  if (!leafletMap.value) return;
  if (isSatellite.value) {
    leafletMap.value.removeLayer(satelliteLayer);
    streetLayer.addTo(leafletMap.value);
  } else {
    leafletMap.value.removeLayer(streetLayer);
    satelliteLayer.addTo(leafletMap.value);
  }
  isSatellite.value = !isSatellite.value;
}

// ── Named location circles ────────────────────────────────────────────────
const locCircleColors: Record<string, string> = {
  depot: "#757575",
  refuel: "#F57C00",
  rest: "#1565C0",
  customer: "#2E7D32",
  other: "#6A1B9A"
};

function clearLocationCircles() {
  locationCircles.forEach((c) => leafletMap.value?.removeLayer(c));
  locationCircles = [];
}

function drawLocationCircles(locations: FleetLocation[]) {
  clearLocationCircles();
  if (!showLocationsPanel.value || !leafletMap.value || !L.value) return;
  for (const loc of locations) {
    if (!loc.lat || !loc.lng) continue;
    const color = locCircleColors[loc.type] ?? "#9E9E9E";
    const circle = L.value
      .circle([loc.lat, loc.lng], {
        radius: loc.radius_m,
        color,
        fillColor: color,
        fillOpacity: 0.15,
        weight: 2
      })
      .addTo(leafletMap.value)
      .bindPopup(
        `<b>${loc.name}</b><br/>${loc.type}` +
          (loc.max_dwell_min != null
            ? `<br/>Max dwell: ${loc.max_dwell_min} min`
            : "<br/>No dwell limit")
      );
    locationCircles.push(circle);
  }
}

function onLocationsUpdated(locs: FleetLocation[]) {
  knownLocations.value = locs;
  drawLocationCircles(locs);
}

function enableMapPickMode() {
  showLocationsPanel.value = true;
  pickingOnMap.value = true;
  if (!leafletMap.value) return;
  leafletMap.value.getContainer().style.cursor = "crosshair";
  leafletMap.value.once("click", (e: any) => {
    pickingOnMap.value = false;
    leafletMap.value.getContainer().style.cursor = "";
    locationsPanelRef.value?.applyMapClick(e.latlng.lat, e.latlng.lng);
  });
}

// ── Build live track from historyRows ─────────────────────────────────────
// ── GPS cleaning ──────────────────────────────────────────────────────────
const MAX_JUMP_KM = 50; // reject GPS jumps > 50 km (bad satellite lock)
const MIN_MOVE_KM = 0.025; // skip points < 25m from last kept point (stationary noise)
const MAX_SPEED_KMH = 80; // reject points implying speed > 80 km/h (cold trucks)

function cleanGpsPoints(raw: LatLng[]): LatLng[] {
  // Filter out 0,0 coordinates (no satellite lock)
  const nonZero = raw.filter((p) => p.lat !== 0 || p.lng !== 0);
  if (!nonZero.length) return [];
  const out: LatLng[] = [nonZero[0]];
  for (let i = 1; i < nonZero.length; i++) {
    const prev = out[out.length - 1];
    const curr = nonZero[i];
    const km = haversineKm(prev, curr);

    if (km < MIN_MOVE_KM) continue; // skip stationary noise

    if (prev.ts && curr.ts) {
      const dtHours = (new Date(curr.ts).getTime() - new Date(prev.ts).getTime()) / 3_600_000;
      if (dtHours > 0) {
        if (km / dtHours > MAX_SPEED_KMH) continue; // speed exceeds limit → GPS noise
      } else if (km > MAX_JUMP_KM) {
        continue; // same/backwards timestamp → use distance cap
      }
    } else if (km > MAX_JUMP_KM) {
      continue; // no timestamps → use distance cap
    }

    out.push(curr);
  }
  return out;
}

// ── Kalman smoother — 1-D Kalman filter applied to lat and lng independently ─
// Q: process noise variance (degrees²) — how much position can drift per step.
//    (10m / 111000 m·deg⁻¹)² ≈ 8e-9; use 1e-8 to allow vehicle movement.
// R: measurement noise variance (degrees²) — GPS accuracy ~±25m.
//    (25 / 111000)² ≈ 5e-8; use 1e-7 to be conservative (noisier GPS = higher R).
// Ratio Q/R ≈ 0.1 → filter mostly trusts smoothed estimate, not raw reading.
function smoothPoints(pts: LatLng[]): LatLng[] {
  if (pts.length < 2) return pts;
  const Q = 1e-8; // process noise
  const R = 3e-7; // measurement noise (higher = trust model more → stronger smoothing)

  function kalman1d(vals: number[]): number[] {
    const out: number[] = [];
    let x = vals[0];
    let P = 1;
    for (const z of vals) {
      const Pp = P + Q; // predicted covariance
      const K = Pp / (Pp + R); // Kalman gain
      x = x + K * (z - x); // corrected estimate
      P = (1 - K) * Pp; // updated covariance
      out.push(x);
    }
    return out;
  }

  const lats = kalman1d(pts.map((p) => p.lat));
  const lngs = kalman1d(pts.map((p) => p.lng));
  return pts.map((p, i) => ({ lat: lats[i], lng: lngs[i], ts: p.ts }));
}

// ── Group consecutive points within 10 m into a centroid ─────────────────
function groupIntoCentroids(pts: LatLng[]): LatLng[] {
  if (!pts.length) return [];
  const CLUSTER_KM = 0.03; // 30 m — matches typical GPS drift radius
  const out: LatLng[] = [];
  let group: LatLng[] = [pts[0]];
  for (let i = 1; i < pts.length; i++) {
    if (haversineKm(group[0], pts[i]) <= CLUSTER_KM) {
      group.push(pts[i]);
    } else {
      const lat = group.reduce((s, p) => s + p.lat, 0) / group.length;
      const lng = group.reduce((s, p) => s + p.lng, 0) / group.length;
      out.push({ lat, lng, ts: group[group.length - 1].ts });
      group = [pts[i]];
    }
  }
  const lat = group.reduce((s, p) => s + p.lat, 0) / group.length;
  const lng = group.reduce((s, p) => s + p.lng, 0) / group.length;
  out.push({ lat, lng, ts: group[group.length - 1].ts });
  return out;
}

// ── Collapse tail dwell zone into a single centroid ───────────────────────
// Scans backward from the last point, collecting all points within dwellKm.
// Allows up to 2 consecutive GPS spikes (> dwellKm) before stopping so that
// multipath outliers inside the dwell cluster don't interrupt the scan.
function collapseTailDwell(pts: LatLng[], dwellKm = 0.2): LatLng[] {
  if (pts.length < 3) return pts;
  const last = pts[pts.length - 1];
  let tailStart = pts.length - 1;
  let consecutiveFar = 0;
  const maxScan = Math.min(pts.length - 1, 60); // look back at most 60 points
  for (let i = pts.length - 2; i >= pts.length - 1 - maxScan; i--) {
    if (haversineKm(last, pts[i]) <= dwellKm) {
      tailStart = i;
      consecutiveFar = 0;
    } else {
      consecutiveFar++;
      if (consecutiveFar >= 3) break; // 3 consecutive far points = left the dwell zone
    }
  }
  if (tailStart >= pts.length - 1) return pts; // only 1 point — nothing to collapse
  const tail = pts.slice(tailStart);
  const centLat = tail.reduce((s, p) => s + p.lat, 0) / tail.length;
  const centLng = tail.reduce((s, p) => s + p.lng, 0) / tail.length;
  return [...pts.slice(0, tailStart), { lat: centLat, lng: centLng, ts: last.ts }];
}

// ── Stationarity detection ─────────────────────────────────────────────────
// Checks whether the device has been effectively stationary by examining
// the DISTRIBUTION of recent raw GPS readings, not their sequential distance.
//
// Uses the MEDIAN of recent readings as the reference (robust to outlier spikes).
// For a parked device with ±150 m GPS multipath (Rayleigh σ=150m):
//   P(within 300m of median) ≈ 86% → well above 70% threshold → STATIONARY
// For a vehicle moving at 10 km/h (readings span 2.5 km over 30 polls):
//   Only ~12% of readings are within 300m of the route's midpoint → MOVING
function isLikelyStationary(rawPts: LatLng[]): boolean {
  // ── Design notes ─────────────────────────────────────────────────────────
  // The backend deduplicates readings, so historyRows only contains unique GPS
  // positions. In a campus/urban environment, GPS multipath routinely jumps
  // 300–500 m while the vehicle is fully parked. This means:
  //   • We may only have 2–20 unique readings over the past 30 min.
  //   • Those readings can cluster at 2 distinct "phantom" positions ~500 m apart.
  //   • The old fraction-based check (70% within 300 m of median) fails because
  //     each cluster is ~275 m from the median — barely outside the 300 m radius —
  //     so only 50 % of readings qualify, not the required 70 %.
  //
  // Solution: single unified check — ALL recent points within 500 m of median.
  // For a genuinely moving truck that drove 1+ km, the extreme readings will be
  // ≥ 500 m from the median and the check returns false (not stationary).
  // For a parked truck with campus multipath clusters 275 m from median: all
  // readings ≤ 500 m → returns true (stationary). ✓

  const WINDOW = 30;
  const MAX_RADIUS_KM = 0.5; // 500 m — covers severe urban/campus GPS multipath

  const recent = rawPts.slice(-WINDOW);
  if (recent.length < 2) return false;

  // Median position — robust to individual outlier spikes
  const sortedLat = recent
    .map((p) => p.lat)
    .slice()
    .sort((a, b) => a - b);
  const sortedLng = recent
    .map((p) => p.lng)
    .slice()
    .sort((a, b) => a - b);
  const med: LatLng = {
    lat: sortedLat[Math.floor(sortedLat.length / 2)],
    lng: sortedLng[Math.floor(sortedLng.length / 2)]
  };

  // All points within MAX_RADIUS_KM of median → parked
  return recent.every((p) => haversineKm(med, p) <= MAX_RADIUS_KM);
}

// ── Find index of most recent trip start ──────────────────────────────────
// Returns -1 if device never moved > 0.05 km from its starting cluster
// (device parked the whole window → don't draw any track).
function findTripStartIndex(pts: LatLng[]): number {
  if (pts.length < 3) return -1;
  const n = Math.min(5, pts.length);
  const clat = pts.slice(0, n).reduce((s, p) => s + p.lat, 0) / n;
  const clng = pts.slice(0, n).reduce((s, p) => s + p.lng, 0) / n;
  const startRef: LatLng = { lat: clat, lng: clng };
  let lastFarIdx = -1;
  for (let i = 0; i < pts.length; i++) {
    if (haversineKm(startRef, pts[i]) > 0.05) lastFarIdx = i;
  }
  if (lastFarIdx === -1) return -1;
  for (let i = lastFarIdx - 1; i >= 0; i--) {
    if (haversineKm(startRef, pts[i]) <= 0.05) return i + 1;
  }
  return 0;
}

function buildLiveTrack() {
  if (!leafletMap.value || !L.value) return;
  if (trackCleared.value) return;

  // 1. Extract raw points, skip 0,0 (no satellite lock)
  //    Limit to the last 2 hours so old GPS positions from earlier locations don't
  //    draw a spider-web across the map when the truck has moved between sites.
  const rawPoints: LatLng[] = [];
  const cutoff = new Date(Date.now() - 2 * 3_600_000).toISOString();
  const recentRows = props.historyRows.filter((r) => r.ts >= cutoff).slice(-300);
  for (const row of recentRows) {
    const ll = extractLatLng(row.raw ?? { latLng: row.latLng });
    if (ll && (ll.lat !== 0 || ll.lng !== 0)) {
      ll.ts = row.ts;
      rawPoints.push(ll);
    }
  }

  // 2. Determine marker position and whether to draw a track.
  //    Stationarity check uses raw reading distribution (median-based) so that
  //    GPS multipath spikes don't make a parked truck look like it's driving.
  const stationary = isLikelyStationary(rawPoints);

  // Compute median of recent raw readings — used as marker position when parked
  // (more accurate than any single spike-prone raw reading).
  const recentRaw = rawPoints.slice(-30);
  const sLat = recentRaw
    .map((p) => p.lat)
    .slice()
    .sort((a, b) => a - b);
  const sLng = recentRaw
    .map((p) => p.lng)
    .slice()
    .sort((a, b) => a - b);
  const medPos: LatLng = {
    lat: sLat[Math.floor(sLat.length / 2)],
    lng: sLng[Math.floor(sLng.length / 2)],
    ts: rawPoints[rawPoints.length - 1].ts
  };

  // 3a. Pipeline (only when moving)
  let allSmoothed: LatLng[] = [];
  if (!stationary) {
    allSmoothed = collapseTailDwell(smoothPoints(groupIntoCentroids(cleanGpsPoints(rawPoints))));
    livePoints.value = allSmoothed;
    if (!allSmoothed.length) {
      liveStatus.value = "No GPS data available";
      return;
    }
  }

  // During a Running trip use the raw latest GPS point (no Kalman lag = closer to actual position).
  // When Stopped/Parked use Kalman-smoothed or median for stability.
  const markerPos = stationary
    ? medPos
    : tripStatus.value === "Running"
      ? rawPoints[rawPoints.length - 1]
      : allSmoothed[allSmoothed.length - 1];
  liveLatest.value = markerPos;
  liveTs.value = markerPos.ts ?? null;

  if (stationary) {
    if (!wasStationary) {
      parkedSince.value = new Date();
      wasStationary = true;
    }
    const parkedMs = parkedSince.value ? Date.now() - parkedSince.value.getTime() : 0;
    const parkedMin = Math.floor(parkedMs / 60000);
    const parkedHr = Math.floor(parkedMin / 60);
    liveStatus.value =
      parkedHr > 0
        ? `Parked ${parkedHr}h ${parkedMin % 60}m`
        : parkedMin >= 1
          ? `Parked ${parkedMin}m`
          : "Parked";
  } else {
    if (wasStationary) {
      parkedSince.value = null;
      wasStationary = false;
    }
    liveStatus.value = "Live tracking…";
  }

  // Avg temp
  const temps = props.historyRows.map((r) => r.temperatureC).filter((v): v is number => v != null);
  avgTemp.value = temps.length ? temps.reduce((a, b) => a + b, 0) / temps.length : null;

  // 3b. Marker (shared between stationary and moving paths)
  const isParked = liveStatus.value.startsWith("Parked");
  const tooltipLabel =
    `<b>${props.truckName ?? props.hardwareId}</b>` +
    (isParked ? `<br/><span style="font-size:10px;color:#e65100">${liveStatus.value}</span>` : "");
  const popupLabel =
    `<b>${props.truckName ?? props.hardwareId}</b><br/>
    ${markerPos.lat.toFixed(6)}, ${markerPos.lng.toFixed(6)}<br/>
    ${markerPos.ts ? fmtTs(markerPos.ts) : ""}` +
    (isParked ? `<br/><span style="color:#e65100">${liveStatus.value}</span>` : "");

  if (!liveMarker) {
    const pulseIcon = L.value.divIcon({
      className: "",
      html: '<div style="width:18px;height:18px;background:#2196F3;border:3px solid white;border-radius:50%;box-shadow:0 0 0 4px rgba(33,150,243,0.35),0 2px 6px rgba(0,0,0,0.3);animation:rtm-pulse 1.8s ease-in-out infinite;"></div>',
      iconSize: [18, 18],
      iconAnchor: [9, 9]
    });
    liveMarker = L.value
      .marker([markerPos.lat, markerPos.lng], { icon: pulseIcon, zIndexOffset: 1000 })
      .addTo(leafletMap.value)
      .bindTooltip(tooltipLabel, {
        permanent: true,
        direction: "top",
        offset: [0, -12],
        className: "rtm-live-label"
      })
      .bindPopup(popupLabel);
  } else {
    liveMarker
      .setLatLng([markerPos.lat, markerPos.lng])
      .setTooltipContent(tooltipLabel)
      .setPopupContent(popupLabel);
  }

  // 4. If parked — suppress track entirely, clear any GPS-noise trail, just show marker.
  //    tripTrail is cleared so that GPS multipath points accumulated while parked
  //    don't reappear as jump lines when stationarity briefly flips to false.
  if (stationary) {
    livePolyline.setLatLngs([]);
    clearBreadcrumbs();
    if (tripTrail.length > 0) {
      tripTrail = [];
      lastTripPoint = null;
    }
    if (!lockTrack.value) leafletMap.value.setView([markerPos.lat, markerPos.lng], 15);
    return;
  }

  // 4b. If no active trip — suppress the historical track to avoid spider-web lines
  //     drawn between sparse historical GPS readings from different locations.
  //     The live marker still updates; the track only draws while a trip is Running.
  if (tripStatus.value !== "Running") {
    livePolyline.setLatLngs([]);
    clearBreadcrumbs();
    if (!lockTrack.value) leafletMap.value.setView([markerPos.lat, markerPos.lng], 15);
    return;
  }

  // Skip track drawing when trip history is locked (saved trip is shown)
  if (lockTrack.value) return;

  // 5. During active trip — draw from the in-memory tripTrail (already distance-gated
  //    by recordTripPoint, so GPS drift noise < 25m is suppressed automatically).
  //    This avoids spider-web lines from sparse historyRows GPS scatter.
  const trailCoords = tripTrail.map((p) => [p.lat, p.lng] as [number, number]);
  if (trailCoords.length < 2) {
    livePolyline.setLatLngs([]);
    clearBreadcrumbs();
    return;
  }

  livePolyline.setLatLngs(trailCoords);

  // Incremental breadcrumbs — only process new trail points since the last poll.
  // Previously: clear all N + re-add all N every 30s. Now: O(new points only).
  if (tripTrail.length > breadcrumbPtIdx) {
    for (let i = breadcrumbPtIdx; i < tripTrail.length; i++) {
      if (i === 0) {
        addBreadcrumb(tripTrail[i], 0);
      } else {
        breadcrumbKmAcc += haversineKm(tripTrail[i - 1], tripTrail[i]);
        if (breadcrumbKmAcc >= 1.0) {
          breadcrumbTotalKm += breadcrumbKmAcc;
          addBreadcrumb(tripTrail[i], breadcrumbTotalKm);
          breadcrumbKmAcc = 0;
        }
      }
    }
    breadcrumbPtIdx = tripTrail.length;
  }
  // Only fit bounds once when the trip trail first becomes visible.
  // Calling fitBounds every poll re-pans/zooms the map every 30s while driving.
  if (trailCoords.length > 1 && !tripBoundsFit) {
    leafletMap.value.fitBounds(trailCoords);
    tripBoundsFit = true;
  }
}

// ── Trip tracking controls ────────────────────────────────────────────────
async function startTrip() {
  if (tripStatus.value === "Running") return;
  tripStatus.value = "Running";
  tripStartTime = new Date();
  tripDistKm.value = 0;
  tripPtCount.value = 0;
  tripTrail = [];
  lastTripPoint = null;
  tripBoundsFit = false;
  saveSuccess.value = false;
  activeTripId.value = null;

  // FIX 3: create an open trip on the server so ingest auto-appends GPS points
  activeTripId.value = await openTrip(props.hardwareId, tripStartTime.toISOString());

  persistTrip();

  // Write actual trip start to shared state so TripRouteIndicator uses real elapsed time
  tripProgressState.value = {
    status: "Running",
    duration: "00:00:00",
    startTimeIso: tripStartTime.toISOString()
  };

  stopAutoDetectTimer();
  startDurationTimer();
  startTripSyncTimer();
}

// Called by the Stop button — shows confirmation first
function requestStopTrip() {
  if (tripStatus.value !== "Running") return;
  showStopConfirm.value = true;
}

// Called after user confirms "Yes, stop"
async function confirmStopTrip() {
  showStopConfirm.value = false;
  if (tripStatus.value !== "Running") return;
  tripStatus.value = "Stopped";
  stopTripSyncTimer();
  if (durationTimer) {
    clearInterval(durationTimer);
    durationTimer = null;
  }

  // When we have a server trip ID, the server has all the GPS data even if
  // the browser was closed — no need to require a local trail.
  if (!tripStartTime) {
    saveError.value = "Trip start time missing — please reset and start again.";
    return;
  }

  if (!activeTripId.value && tripTrail.length < 1) {
    saveError.value = "No GPS points recorded yet — make sure the device has GPS signal.";
    return;
  }

  const endTime = new Date().toISOString();
  let result;

  if (activeTripId.value) {
    // Server recalculates distance from all accumulated points (including those
    // collected while the browser was closed)
    result = await closeTrip(activeTripId.value, endTime);
  } else {
    // Fallback: save locally-collected points
    result = await saveTrip(
      props.hardwareId,
      tripStartTime.toISOString(),
      endTime,
      tripDistKm.value,
      tripTrail
    );
  }
  activeTripId.value = null;
  serverPtCount.value = null;

  clearPersistedTrip();

  if (result) {
    saveSuccess.value = true;
    await fetchTrips(props.hardwareId);
  }
  startAutoDetectTimer();
}

function resetTrip() {
  if (durationTimer) {
    clearInterval(durationTimer);
    durationTimer = null;
  }
  stopTripSyncTimer();
  tripStatus.value = "Stopped";
  tripDistKm.value = 0;
  tripPtCount.value = 0;
  tripDuration.value = "00:00:00";
  tripStartTime = null;
  tripTrail = [];
  lastTripPoint = null;
  tripBoundsFit = false;
  saveSuccess.value = false;
  saveError.value = null;
  tripProgressState.value = { status: "Stopped", duration: "00:00:00", startTimeIso: null };
  activeTripId.value = null;
  clearPersistedTrip();
}

// ── Auto-detect / sync timers ─────────────────────────────────────────────
// autoDetectTimer (30s, Stopped): polls the server for a scheduler-opened trip.
//   When found, syncs the UI to Running and starts the duration timer.
// tripSyncTimer (60s, Running): polls the server to check the active trip is still open.
//   If the scheduler closed it, resets the UI to Stopped.

function stopAutoDetectTimer() {
  if (autoDetectTimer) {
    clearInterval(autoDetectTimer);
    autoDetectTimer = null;
  }
}
function stopTripSyncTimer() {
  if (tripSyncTimer) {
    clearInterval(tripSyncTimer);
    tripSyncTimer = null;
  }
}

function startDurationTimer() {
  if (durationTimer) return;
  durationTimer = setInterval(() => {
    if (!tripStartTime) return;
    const ms = Date.now() - tripStartTime.getTime();
    const h = Math.floor(ms / 3600000);
    const m = Math.floor((ms % 3600000) / 60000);
    const s = Math.floor((ms % 60000) / 1000);
    tripDuration.value = [h, m, s].map((n) => String(n).padStart(2, "0")).join(":");
    if (Math.floor(ms / 1000) % 30 === 0) persistTrip();
  }, 1000);
}

async function checkForSchedulerTrip() {
  if (tripStatus.value === "Running") return;
  await fetchTrips(props.hardwareId);
  const active = trips.value.find((t) => !t.endTime);
  if (!active) return;

  // Scheduler opened a trip — sync UI without calling openTrip() again
  stopAutoDetectTimer();
  activeTripId.value = active.tripId;
  tripStartTime = new Date(active.startTime);
  tripDistKm.value = active.totalDistanceKm;
  tripPtCount.value = active.pointsCount;
  tripStatus.value = "Running";
  saveSuccess.value = false;
  tripProgressState.value = {
    status: "Running",
    duration: "00:00:00",
    startTimeIso: tripStartTime.toISOString()
  };
  persistTrip();
  startDurationTimer();
  startTripSyncTimer();
}

async function checkTripStillActive() {
  if (tripStatus.value !== "Running" || !activeTripId.value) return;
  await fetchTrips(props.hardwareId);
  const trip = trips.value.find((t) => t.tripId === activeTripId.value);
  if (!trip || trip.endTime != null) {
    // Scheduler closed the trip while we were Running — reset and re-arm detect timer
    stopTripSyncTimer();
    resetTrip();
    await fetchTrips(props.hardwareId);
    startAutoDetectTimer();
  }
}

function startAutoDetectTimer() {
  stopAutoDetectTimer();
  autoDetectTimer = setInterval(checkForSchedulerTrip, 30_000);
}

function startTripSyncTimer() {
  stopTripSyncTimer();
  tripSyncTimer = setInterval(checkTripStillActive, 60_000);
}

function clearMapTrack() {
  trackCleared.value = true;
  lockTrack.value = false;
  livePolyline?.setLatLngs([]);
  tripPolyline?.setLatLngs([]);
  clearBreadcrumbs();
  if (startMarker) {
    leafletMap.value?.removeLayer(startMarker);
    startMarker = null;
  }
  if (endMarker) {
    leafletMap.value?.removeLayer(endMarker);
    endMarker = null;
  }
}

// Accumulate points while trip is running — called every time liveLatest updates
function recordTripPoint() {
  if (tripStatus.value !== "Running") return;
  // Don't record when the device is detected as stationary — GPS multipath can jump
  // hundreds of meters between readings even when parked, which causes straight-line
  // spikes in the trip trail.
  if (liveStatus.value === "Parked") return;
  const latest = liveLatest.value;
  if (!latest) return;

  if (lastTripPoint) {
    const km = haversineKm(lastTripPoint, latest);
    if (km < MIN_MOVE_KM) return; // ignore stationary jitter
    // Speed gate — same threshold as cleanGpsPoints
    if (lastTripPoint.ts && latest.ts) {
      const dtHours =
        (new Date(latest.ts).getTime() - new Date(lastTripPoint.ts).getTime()) / 3_600_000;
      if (dtHours > 0 && km / dtHours > MAX_SPEED_KMH) return;
    }
    tripDistKm.value += km;
  }

  tripPtCount.value++;
  lastTripPoint = { ...latest };

  const lastRow = props.historyRows[props.historyRows.length - 1];
  tripTrail.push({
    lat: latest.lat,
    lng: latest.lng,
    ts: latest.ts ?? new Date().toISOString(),
    temp: lastRow?.temperatureC ?? null
  });
  persistTrip();
}

// Watch liveLatest — fires every time buildLiveTrack updates the position
watch(liveLatest, (newVal, oldVal) => {
  if (!newVal) return;
  // Only record if coordinates actually changed
  if (oldVal && newVal.lat === oldVal.lat && newVal.lng === oldVal.lng) return;
  recordTripPoint();
});

// ── Trip history: load & show ─────────────────────────────────────────────
async function handleSelectTrip(tripId: number) {
  selectedHistoryId.value = tripId;
  await fetchTripPoints(tripId);

  if (!tripPoints.value.length || !leafletMap.value || !L.value) return;

  lockTrack.value = true;
  if (liveMarker) liveMarker.closePopup(); // hide live popup so it doesn't cover trip markers

  // Saved trip points are already pre-filtered during recording — only smooth, no distance gate
  const rawLatLng: LatLng[] = tripPoints.value.map((p) => ({ lat: p.lat, lng: p.lng, ts: p.ts }));
  const smoothed = smoothPoints(rawLatLng);
  const coords = smoothed.map((p) => [p.lat, p.lng] as [number, number]);
  tripPolyline.setLatLngs(coords);

  if (startMarker) leafletMap.value.removeLayer(startMarker);
  if (endMarker) leafletMap.value.removeLayer(endMarker);

  const first = smoothed[0] ?? tripPoints.value[0];
  const last = smoothed[smoothed.length - 1] ?? tripPoints.value[tripPoints.value.length - 1];

  // min-width + text-align:center give both badges the same fixed width so iconAnchor [26,24]
  // reliably centres them horizontally. Bottom-anchor (y=24) makes both float ABOVE their point.
  const badgeHtml = (color: string, label: string) =>
    `<div style="background:${color};color:white;font-weight:bold;font-size:11px;padding:3px 7px;border-radius:4px;white-space:nowrap;border:2px solid white;box-shadow:0 2px 6px rgba(0,0,0,0.4);min-width:52px;text-align:center;">${label}</div>`;

  // Start badge — bottom-center anchored → floats ABOVE the first polyline point
  startMarker = L.value
    .marker([first.lat, first.lng], {
      icon: L.value.divIcon({
        className: "",
        html: badgeHtml("#16A34A", "Start"),
        iconAnchor: [26, 24]
      }),
      zIndexOffset: 1100
    })
    .addTo(leafletMap.value);

  // End badge — bottom-center anchored → floats ABOVE the last polyline point
  endMarker = L.value
    .marker([last.lat, last.lng], {
      icon: L.value.divIcon({
        className: "",
        html: badgeHtml("#DC2626", "End"),
        iconAnchor: [26, 24]
      }),
      zIndexOffset: 1200
    })
    .addTo(leafletMap.value);

  leafletMap.value.fitBounds(coords);
}

// ── Helpers ───────────────────────────────────────────────────────────────
function clearBreadcrumbs() {
  breadcrumbs.forEach((b) => leafletMap.value?.removeLayer(b));
  breadcrumbs = [];
  breadcrumbPtIdx = 0;
  breadcrumbKmAcc = 0;
  breadcrumbTotalKm = 0;
}

function addBreadcrumb(p: LatLng, km: number) {
  if (!leafletMap.value || !L.value) return;
  const b = L.value
    .circleMarker([p.lat, p.lng], {
      radius: 5,
      color: "#FF9800",
      weight: 2,
      fillColor: "#FF9800",
      fillOpacity: 0.8
    })
    .addTo(leafletMap.value)
    .bindPopup(`${km.toFixed(2)} km`);
  breadcrumbs.push(b);
}

function haversineKm(a: LatLng, b: LatLng): number {
  const R = 6371;
  const toRad = (d: number) => (d * Math.PI) / 180;
  const dLat = toRad(b.lat - a.lat);
  const dLng = toRad(b.lng - a.lng);
  const s =
    Math.sin(dLat / 2) ** 2 +
    Math.cos(toRad(a.lat)) * Math.cos(toRad(b.lat)) * Math.sin(dLng / 2) ** 2;
  return R * 2 * Math.atan2(Math.sqrt(s), Math.sqrt(1 - s));
}

// ── Watchers ──────────────────────────────────────────────────────────────
// Shallow watch is enough — fetchHistory always assigns a new array reference.
// deep:true would traverse every row object on each 30s poll, which is wasteful.
watch(
  () => props.historyRows,
  () => buildLiveTrack()
);

watch(showLocationsPanel, (visible) => {
  if (visible) {
    drawLocationCircles(knownLocations.value);
  } else {
    clearLocationCircles();
    if (pickingOnMap.value) {
      pickingOnMap.value = false;
      const el = leafletMap.value?.getContainer();
      if (el) el.style.cursor = "";
    }
  }
});

watch(
  () => props.hardwareId,
  async (hw) => {
    if (!hw) return;

    // ── Clear all map state from previous device ──────────────────────────
    livePoints.value = [];
    liveLatest.value = null;
    liveTs.value = null;
    liveStatus.value = "Waiting for device…";
    avgTemp.value = null;

    livePolyline?.setLatLngs([]);
    tripPolyline?.setLatLngs([]);

    if (liveMarker) {
      leafletMap.value?.removeLayer(liveMarker);
      liveMarker = null;
    }
    if (startMarker) {
      leafletMap.value?.removeLayer(startMarker);
      startMarker = null;
    }
    if (endMarker) {
      leafletMap.value?.removeLayer(endMarker);
      endMarker = null;
    }

    clearBreadcrumbs();
    clearLocationCircles();

    // Reset flags so live tracking draws normally on the new device
    lockTrack.value = false;
    trackCleared.value = false;
    selectedHistoryId.value = null;

    // Reset parked tracking
    wasStationary = false;
    parkedSince.value = null;

    // Reset trip trail so stale points from the previous device don't bleed through
    tripTrail = [];
    lastTripPoint = null;
    breadcrumbPtIdx = 0;
    breadcrumbKmAcc = 0;
    breadcrumbTotalKm = 0;

    // Load trips for the newly selected device
    await fetchTrips(hw);
  }
);

// ── Lifecycle ─────────────────────────────────────────────────────────────
onMounted(async () => {
  sweepStaleStorageKeys();
  await nextTick();
  await initMap();
  buildLiveTrack();

  // Keep screen/tab alive while map is open
  acquireWakeLock();
  document.addEventListener("visibilitychange", onVisibilityChange);

  // Restore active trip if page was reloaded or browser was closed mid-trip
  if (restorePersistedTrip()) {
    tripRecovered.value = true;
    // Restore shared progress state so TripRouteIndicator continues from real start time
    tripProgressState.value = {
      status: "Running",
      duration: tripDuration.value,
      startTimeIso: tripStartTime?.toISOString() ?? null
    };
    startDurationTimer();
    startTripSyncTimer();
    // Immediately re-evaluate stationarity on the restored trail so that GPS-noise
    // points accumulated before a page reload are cleared right away — without waiting
    // up to 30s for the next historyRows update to trigger buildLiveTrack.
    buildLiveTrack();

    // Fetch server-side trip stats so we can show how many points were collected
    // by the ingest service while the browser was closed
    if (activeTripId.value) {
      fetchTrips(props.hardwareId).then(() => {
        const serverTrip = trips.value.find((t) => t.tripId === activeTripId.value);
        if (serverTrip) {
          serverPtCount.value = serverTrip.pointsCount;
          tripDistKm.value = serverTrip.totalDistanceKm;
        }
      });
    }
  } else {
    // No active trip locally — check the server immediately (don't wait 30s for first timer tick)
    // then keep polling in case the scheduler starts one later.
    void checkForSchedulerTrip();
    startAutoDetectTimer();
  }
});

onUnmounted(() => {
  document.removeEventListener("visibilitychange", onVisibilityChange);
  releaseWakeLock();
  // Persist trip state before unmounting so it survives navigation to other pages
  persistTrip();
  leafletMap.value?.remove();
  if (durationTimer) clearInterval(durationTimer);
  stopAutoDetectTimer();
  stopTripSyncTimer();
});
</script>

<template>
  <div class="row" :class="$style.mapContainer">
    <!-- ── Map ─────────────────────────────────────────────────────── -->
    <div class="col" :class="$style.mapCol">
      <!-- Top banner -->
      <div :class="$style.mapOverlayBanner">
        <div :class="$style.bannerLeft">
          <q-icon name="fa-solid fa-location-dot" color="primary" size="13px" />
          <b>{{ props.truckName ?? props.hardwareId }}</b>
          <span class="text-grey-4">|</span>
          <span :class="$style.bannerTs">{{ liveTs ? fmtTs(liveTs) : "—" }}</span>
          <template v-if="avgTemp != null">
            <span class="text-grey-4">|</span>
            <q-icon name="fa-solid fa-temperature-half" color="blue-6" size="11px" />
            <span :class="$style.bannerTemp">
              {{ avgTemp.toFixed(1) }}°C avg
            </span>
          </template>
        </div>
        <div :class="$style.bannerRight">
          <q-badge
            :color="
              liveStatus === 'Live tracking…'
                ? 'positive'
                : liveStatus.startsWith('Parked')
                  ? 'orange-7'
                  : 'grey-5'
            "
            :label="liveStatus"
            style="font-size: 10px"
          />
          <q-btn
            flat
            dense
            no-caps
            size="sm"
            :icon="isSatellite ? 'fa-solid fa-map' : 'fa-solid fa-satellite'"
            :label="isSatellite ? 'Street' : 'Satellite'"
            color="grey-8"
            :class="$style.toolbarBtn"
            @click="toggleTileLayer"
          />
          <q-btn
            flat
            dense
            no-caps
            size="sm"
            icon="fa-solid fa-crosshairs"
            label="Locate"
            color="primary"
            :class="$style.toolbarBtn"
            :disable="!liveLatest"
            @click="locateDevice"
          />
          <q-btn
            flat
            dense
            no-caps
            size="sm"
            icon="fa-solid fa-trash-can"
            label="Clear Track"
            color="orange-8"
            :class="$style.toolbarBtn"
            @click="clearMapTrack"
          />
          <q-btn
            flat
            dense
            no-caps
            size="sm"
            icon="fa-solid fa-map-pin"
            label="Locations"
            :color="showLocationsPanel ? 'primary' : 'grey-8'"
            :class="$style.toolbarBtn"
            @click="showLocationsPanel = !showLocationsPanel"
          />
        </div>
      </div>

      <!-- Pick-on-map hint overlay -->
      <div v-if="pickingOnMap" :class="$style.pickHint">
        <q-icon name="fa-solid fa-crosshairs" class="q-mr-xs" size="13px" />
        Click on the map to pick a location
      </div>

      <div ref="mapEl" :class="$style.leafletCanvas" />
    </div>

    <!-- ── Right panel ────────────────────────────────────────────── -->
    <div :class="$style.rightPanel">
      <!-- ── Location Tracking ───────────────────────────────────── -->
      <div :class="$style.sectionNoShrink">
        <div :class="[$style.sectionHeader, 'headerColor text-weight-bold q-px-md q-py-sm']">
          <q-icon name="fa-solid fa-satellite-dish" size="12px" class="q-mr-xs" />
          Location Tracking
        </div>
        <q-separator />
        <div class="q-px-md q-py-sm">
          <table :class="$style.infoTable">
            <tr>
              <td :class="$style.labelCell">Device</td>
              <td :class="$style.valueCellBreak">{{ props.hardwareId }}</td>
            </tr>
            <tr v-if="props.truckName">
              <td :class="$style.labelCell">Truck</td>
              <td :class="$style.valueCell">{{ props.truckName }}</td>
            </tr>
            <tr>
              <td :class="$style.labelCell">Last Update</td>
              <td :class="$style.valueCellSmall">{{ liveTs ? fmtTs(liveTs) : "—" }}</td>
            </tr>
            <tr>
              <td :class="$style.labelCell">Latitude</td>
              <td :class="$style.valueCellMono">{{ liveLatest ? liveLatest.lat.toFixed(6) : "—" }}</td>
            </tr>
            <tr>
              <td :class="$style.labelCell">Longitude</td>
              <td :class="$style.valueCellMono">{{ liveLatest ? liveLatest.lng.toFixed(6) : "—" }}</td>
            </tr>
            <tr v-if="avgTemp != null">
              <td :class="$style.labelCell">Avg Temp</td>
              <td :class="$style.valueCellBold">{{ avgTemp.toFixed(1) }} °C</td>
            </tr>
          </table>
        </div>
      </div>

      <!-- ── Trip Tracking ────────────────────────────────────────── -->
      <div :class="$style.sectionDividerFlex">
        <div :class="[$style.sectionHeader, 'headerColor text-weight-bold q-px-md q-py-sm']">
          <q-icon name="fa-solid fa-route" size="12px" class="q-mr-xs" />
          Trip Tracking
        </div>
        <q-separator />
        <div class="q-px-md q-py-sm">
          <!-- Status bar -->
          <div
            :class="[
              $style.tripStatusBar,
              tripStatus === 'Running' ? $style.tripStatusRunning : $style.tripStatusStopped
            ]"
          >
            <div class="row items-center q-gutter-xs text-weight-bold text-caption">
              <q-icon
                :name="tripStatus === 'Running' ? 'fa-solid fa-circle-dot' : 'fa-regular fa-circle'"
                :color="tripStatus === 'Running' ? 'positive' : 'grey-5'"
                size="11px"
              />
              {{ tripStatus }}
            </div>
            <span :class="$style.tripTimerText">{{ tripDuration }}</span>
          </div>

          <!-- Stats -->
          <table :class="$style.infoTable" class="q-mb-sm">
            <tr>
              <td :class="$style.labelCell">Distance</td>
              <td :class="$style.valueCellBold">{{ tripDistKm.toFixed(2) }} km</td>
            </tr>
            <tr>
              <td :class="$style.labelCell">Points</td>
              <td :class="$style.valueCell">{{ tripPtCount }}</td>
            </tr>
            <tr>
              <td :class="$style.labelCell">Avg Temp</td>
              <td :class="$style.valueCell">{{ avgTemp != null ? `${avgTemp.toFixed(1)} °C` : "—" }}</td>
            </tr>
          </table>

          <!-- Recovery banner — dismissible by user -->
          <q-banner
            v-if="tripRecovered"
            dense
            rounded
            class="q-mb-sm bg-orange-1 text-orange-9"
            :class="$style.recoveryBanner"
          >
            <template #avatar>
              <q-icon name="fa-solid fa-rotate-right" size="11px" />
            </template>
            Trip restored after reload —
            <template v-if="serverPtCount != null">
              {{ serverPtCount }} points collected server-side
            </template>
            <template v-else>{{ tripPtCount }} points recovered</template>
            <template #action>
              <q-btn flat dense no-caps size="xs" label="Dismiss" @click="tripRecovered = false" />
            </template>
          </q-banner>

          <!-- Start / Stop / Reset buttons -->
          <div class="row q-gutter-xs">
            <q-btn
              no-caps
              dense
              class="col"
              color="positive"
              icon="fa-solid fa-play"
              label="Start"
              :disable="tripStatus === 'Running'"
              @click="startTrip"
            />
            <q-btn
              no-caps
              dense
              class="col"
              color="negative"
              icon="fa-solid fa-stop"
              label="Stop"
              :disable="tripStatus === 'Stopped'"
              :loading="saveLoading"
              @click="requestStopTrip"
            />
            <q-btn
              no-caps
              dense
              class="col"
              outline
              color="grey-7"
              icon="fa-solid fa-rotate"
              label="Reset"
              @click="resetTrip"
            />
          </div>

          <!-- Save feedback -->
          <div v-if="saveSuccess" class="q-mt-xs text-positive text-caption">
            <q-icon name="fa-solid fa-circle-check" class="q-mr-xs" />
            Trip saved to database!
          </div>
          <div v-if="saveError" class="q-mt-xs text-negative text-caption">
            {{ saveError }}
          </div>
        </div>
      </div>

      <!-- ── Named Locations panel ─────────────────────────────────── -->
      <div v-if="showLocationsPanel" :class="$style.sectionDividerFlex">
        <LocationsPanel
          ref="locationsPanelRef"
          @update="onLocationsUpdated"
          @pick-on-map="enableMapPickMode"
        />
      </div>

      <!-- ── Trip History ──────────────────────────────────────────── -->
      <div :class="$style.sectionDividerFlexGrow">
        <div :class="[$style.sectionHeaderFlex, 'headerColor text-weight-bold q-px-md q-py-sm']">
          <span>
            <q-icon name="fa-solid fa-clock-rotate-left" size="12px" class="q-mr-xs" />
            Trip History
          </span>
          <q-btn
            flat
            dense
            round
            size="xs"
            icon="fa-solid fa-arrows-rotate"
            color="grey-7"
            :loading="tripsLoading"
            @click="fetchTrips(hardwareId)"
          />
        </div>
        <q-separator />
        <div class="q-px-md q-py-sm" :class="$style.tripHistoryContent">
          <q-btn
            v-if="lockTrack"
            no-caps
            dense
            outline
            color="primary"
            icon="fa-solid fa-satellite-dish"
            label="Back to Live"
            class="q-mb-sm"
            @click="backToLive"
          />

          <div v-if="tripsError" class="text-negative text-caption q-mb-xs">{{ tripsError }}</div>

          <!-- Trip list — click a row to select and show on map -->
          <div :class="$style.tripScroll">
            <q-inner-loading :showing="tripsLoading" size="24px" />
            <div
              v-for="t in trips"
              :key="t.tripId"
              class="q-py-sm q-px-sm"
              :class="$style.tripListItem"
              :style="{ background: selectedHistoryId === t.tripId ? '#EDE9FE' : 'transparent' }"
              @click="handleSelectTrip(t.tripId)"
            >
              <div class="row items-center justify-between q-mb-xs">
                <span class="text-purple-7 text-weight-bold text-caption">
                  #{{ t.tripId }}
                </span>
                <q-badge
                  :color="t.endTime ? 'grey-5' : 'positive'"
                  :label="t.endTime ? 'Done' : 'Active'"
                  style="font-size: 9px"
                />
              </div>
              <div :class="$style.tripItemDate">{{ fmtTs(t.startTime) }}</div>
              <div :class="$style.tripItemMeta">
                <span>
                  <q-icon name="fa-solid fa-road" size="9px" class="q-mr-xs" />
                  {{ t.totalDistanceKm.toFixed(2) }} km
                </span>
                <span>
                  <q-icon name="fa-solid fa-clock" size="9px" class="q-mr-xs" />
                  {{ tripDurationLabel(t.startTime, t.endTime) }}
                </span>
                <span>
                  <q-icon name="fa-solid fa-location-dot" size="9px" class="q-mr-xs" />
                  {{ t.pointsCount }} pts
                </span>
              </div>
            </div>

            <div
              v-if="!trips.length && !tripsLoading"
              class="text-center q-pa-md"
              :class="$style.tripNoData"
            >
              No saved trips yet
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>

  <!-- Stop trip confirmation dialog -->
  <q-dialog v-model="showStopConfirm" persistent>
    <q-card style="min-width: 320px">
      <q-card-section class="row items-center q-pb-none">
        <q-icon
          name="fa-solid fa-triangle-exclamation"
          color="warning"
          size="24px"
          class="q-mr-sm"
        />
        <span class="text-weight-bold" style="font-size: 15px">Stop Trip</span>
      </q-card-section>
      <q-card-section>
        Are you sure you want to stop and save this trip? The trip will be closed and saved to the
        database.
      </q-card-section>
      <q-card-actions align="right">
        <q-btn flat no-caps label="Cancel" color="grey-7" @click="showStopConfirm = false" />
        <q-btn
          no-caps
          label="Yes, Stop"
          color="negative"
          :loading="saveLoading"
          @click="confirmStopTrip"
        />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<style lang="sass" module>
@import '../../../../style/_variables'

.mapContainer
  height: 600px
  overflow: hidden

.rightPanel
  width: 310px
  height: 600px
  border-left: 1px solid $secondary-grey-2
  background: $white
  display: flex
  flex-direction: column
  overflow: hidden

.sectionHeader
  font-size: 12px
  color: $primary-black
  text-transform: uppercase
  letter-spacing: 0.4px

.sectionHeaderFlex
  font-size: 12px
  color: $primary-black
  text-transform: uppercase
  letter-spacing: 0.4px
  display: flex
  align-items: center
  justify-content: space-between

.sectionDividerFlex
  border-top: 1px solid $secondary-grey-2
  flex-shrink: 0

.sectionDividerFlexGrow
  border-top: 1px solid $secondary-grey-2
  flex: 1
  min-height: 0
  display: flex
  flex-direction: column

.labelCell
  color: $secondary-grey-1
  padding: 3px 0
  width: 80px

.valueCell
  text-align: right
  color: $primary-black

.valueCellBold
  text-align: right
  color: $primary-black
  font-weight: 600

.valueCellSmall
  text-align: right
  color: $primary-black
  font-size: 10px

.valueCellBreak
  text-align: right
  color: $primary-black
  font-size: 11px
  word-break: break-all

.valueCellMono
  text-align: right
  color: $primary-black
  font-variant-numeric: tabular-nums

.tripStatusBar
  border-radius: 6px
  padding: 7px 10px
  margin-bottom: 8px
  display: flex
  justify-content: space-between
  align-items: center

.tripStatusRunning
  background: #e8f5e9
  border: 1px solid #a5d6a7

.tripStatusStopped
  background: $primary-grey-1
  border: 1px solid $secondary-grey-2

.tripTimerText
  font-size: 12px
  color: $primary-black
  font-variant-numeric: tabular-nums
  font-weight: 500

.tripListItem
  font-size: 11px
  border-bottom: 1px solid $secondary-grey-2
  cursor: pointer
  border-radius: 4px

.tripItemDate
  color: $primary-black
  font-size: 11px

.tripItemMeta
  display: flex
  gap: 8px
  color: $secondary-grey-1
  font-size: 10px
  margin-top: 2px

.tripNoData
  color: $secondary-grey-1
  font-size: 12px

.bannerTs
  color: $primary-black
  font-size: 11px

.bannerTemp
  font-size: 11px
  font-weight: 600
  color: $primary-black

.mapCol
  position: relative
  min-width: 0

.mapOverlayBanner
  position: absolute
  top: 10px
  left: 10px
  right: 10px
  z-index: 1000
  background: rgba(255, 255, 255, 0.95)
  border: 1px solid $secondary-grey-2
  border-radius: 8px
  padding: 7px 12px
  font-size: 12px
  display: flex
  justify-content: space-between
  align-items: center
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1)

.bannerLeft
  display: flex
  align-items: center
  gap: 8px

.bannerRight
  display: flex
  align-items: center
  gap: 6px

.toolbarBtn
  font-size: 11px

.pickHint
  position: absolute
  bottom: 16px
  left: 50%
  transform: translateX(-50%)
  z-index: 1001
  background: rgba(25, 118, 210, 0.9)
  color: white
  padding: 7px 16px
  border-radius: 20px
  font-size: 12px
  font-weight: 600
  pointer-events: none
  white-space: nowrap

.leafletCanvas
  height: 100%
  width: 100%

.sectionNoShrink
  flex-shrink: 0

.infoTable
  width: 100%
  font-size: 12px
  border-collapse: collapse

.tripHistoryContent
  flex: 1
  min-height: 0
  display: flex
  flex-direction: column

.tripScroll
  overflow-y: auto
  flex: 1
  min-height: 0

.recoveryBanner
  font-size: 11px
</style>

<style>
@keyframes rtm-pulse {
  0% {
    box-shadow:
      0 0 0 4px rgba(33, 150, 243, 0.35),
      0 2px 6px rgba(0, 0, 0, 0.3);
  }
  50% {
    box-shadow:
      0 0 0 10px rgba(33, 150, 243, 0),
      0 2px 6px rgba(0, 0, 0, 0.3);
  }
  100% {
    box-shadow:
      0 0 0 4px rgba(33, 150, 243, 0.35),
      0 2px 6px rgba(0, 0, 0, 0.3);
  }
}
</style>
