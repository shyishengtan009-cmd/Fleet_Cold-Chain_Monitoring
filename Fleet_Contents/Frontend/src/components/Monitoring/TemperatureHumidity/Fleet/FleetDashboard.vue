<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from "vue";
import { useRouter } from "vue-router";

import api from "@/helpers/api";

import HeaderAppV2 from "@/components/common/HeaderAppV2.vue";
import LabelApp from "@/components/common/LabelApp.vue";
import DeviceDropdown from "../DeviceDropdown.vue";
import FleetCharts, { type AggregatedRow, type HistoryRow } from "./FleetCharts.vue";
import FleetDeviceHistory from "./FleetDeviceHistory.vue";
import FleetStatus, { type FleetItem } from "./FleetStatus.vue";
import { useAlarmNotifier } from "../FleetRealTimeMonitoring/useAlarmNotifier";
import FleetDevicePortal from "../FleetRealTimeMonitoring/FleetDevicePortal.vue";
import { useFleetDeviceSelection } from "./useFleetDeviceSelection";

// ─── Router ───────────────────────────────────────────────────────────────────
const router = useRouter();

// ─── Types ────────────────────────────────────────────────────────────────────
interface FleetResponse {
  nowUtc: string;
  warnSeconds: number;
  offlineSeconds: number;
  count: number;
  items: FleetItem[];
}

interface HistoryRangeResponse {
  hardwareId: string;
  count: number;
  rows: HistoryRow[];
}

// ─── Config ───────────────────────────────────────────────────────────────────
const TIMEZONE = "Asia/Kuala_Lumpur";
const FLEET_REFRESH_MS   = 30 * 1000;        // live status table — matches RTM poll rate
const HISTORY_REFRESH_MS = 5 * 60 * 1000;   // history/charts — less frequent is fine
const WARN_SEC = 15 * 60;
const OFFLINE_SEC = 30 * 60;
const AGG_THRESHOLD_HOURS = 24; // use aggregated endpoint when range exceeds this

// ─── Fleet State ──────────────────────────────────────────────────────────────
const fleetItems = ref<FleetItem[]>([]);
const fleetLoading = ref(false);
const fleetError = ref<string | null>(null);
const fleetUpdated = ref<string | null>(null);
const { selectedHardwareId: selectedDevice, setDevice } = useFleetDeviceSelection();
const showPortal = ref(false);
const { start: startAlarmNotifier } = useAlarmNotifier();

async function onDeviceRegistered(hardwareId: string) {
  showPortal.value = false;
  await fetchFleet();
  if (hardwareId) setDevice(hardwareId);
}

let fleetTimer: ReturnType<typeof setInterval> | null = null;
let historyTimer: ReturnType<typeof setInterval> | null = null;

// ─── Last-updated badge ───────────────────────────────────────────────────────
const lastUpdatedAt = ref<number | null>(null);
const lastUpdatedAgo = ref<string>("");
let agoTimer: ReturnType<typeof setInterval> | null = null;

function updateAgo() {
  if (!lastUpdatedAt.value) { lastUpdatedAgo.value = ""; return; }
  const secs = Math.floor((Date.now() - lastUpdatedAt.value) / 1000);
  if (secs < 60) lastUpdatedAgo.value = `${secs}s ago`;
  else if (secs < 3600) lastUpdatedAgo.value = `${Math.floor(secs / 60)}m ago`;
  else lastUpdatedAgo.value = `${Math.floor(secs / 3600)}h ago`;
}

// ─── History State ────────────────────────────────────────────────────────────
const historyLoading = ref(false);
const historyError = ref<string | null>(null);
const historyMeta = ref<string>("");
const historyRows = ref<HistoryRow[]>([]);
const aggregatedRows = ref<AggregatedRow[]>([]);
const startUtc = ref("");
const endUtc = ref("");
const metaMinTs = ref<string | null>(null);
const metaMaxTs = ref<string | null>(null);
const startProxyRef = ref<{ hide: () => void } | null>(null);
const endProxyRef = ref<{ hide: () => void } | null>(null);
function onDateScroll() { startProxyRef.value?.hide(); endProxyRef.value?.hide(); }
function onDateShow() { window.addEventListener("scroll", onDateScroll, true); }
function onDateHide() { window.removeEventListener("scroll", onDateScroll, true); }

const startUtcDate = computed(() => startUtc.value.slice(0, 10));
const endUtcDate = computed(() => endUtc.value.slice(0, 10));

// Discoverability hint: how much history exists beyond today's default view,
// so a first-time viewer knows "Full Range" is worth clicking. Only relevant
// while the currently-displayed range actually IS just today — once "Full
// Range" (or a manual query) changes it, the hint would be misleading.
const fullRangeDays = computed(() => {
  if (!metaMinTs.value || !metaMaxTs.value) return 0;
  const ms = new Date(metaMaxTs.value).getTime() - new Date(metaMinTs.value).getTime();
  return Math.max(1, Math.ceil(ms / (24 * 3_600_000)));
});

const isShowingToday = computed(() => {
  const { start, end } = todayMytRange();
  return startUtc.value === isoToDisplayDt(start) && endUtc.value === isoToDisplayDt(end);
});

const isShowingFullRange = computed(() => {
  if (!metaMinTs.value || !metaMaxTs.value) return false;
  return (
    startUtc.value === isoToDisplayDt(metaMinTs.value) &&
    endUtc.value === isoToDisplayDt(metaMaxTs.value)
  );
});

function setStartDate(v: string) {
  startUtc.value = v;
}
function setEndDate(v: string) {
  endUtc.value = v;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────
function formatTime(iso: string | null): string {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleString("en-US", {
      timeZone: TIMEZONE,
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
      hour12: true
    });
  } catch {
    return iso;
  }
}

function isoToDisplayDt(iso: string | null): string {
  if (!iso) return "";
  const d = new Date(iso);
  if (isNaN(d.getTime())) return "";
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${pad(d.getUTCDate())}/${pad(d.getUTCMonth() + 1)}/${d.getUTCFullYear()}`;
}

function displayDateToStartIso(val: string): string {
  if (!val || val.includes("_")) return "";
  const [dd, mm, yyyy] = val.split("/");
  if (!dd || !mm || !yyyy || yyyy.length < 4) return "";
  const d = new Date(Date.UTC(+yyyy, +mm - 1, +dd, 0, 0, 0));
  return isNaN(d.getTime()) ? "" : d.toISOString();
}

function displayDateToEndIso(val: string): string {
  if (!val || val.includes("_")) return "";
  const [dd, mm, yyyy] = val.split("/");
  if (!dd || !mm || !yyyy || yyyy.length < 4) return "";
  const d = new Date(Date.UTC(+yyyy, +mm - 1, +dd, 23, 59, 59));
  return isNaN(d.getTime()) ? "" : d.toISOString();
}

function todayMytRange() {
  const now = new Date();
  const y = now.getUTCFullYear();
  const mo = now.getUTCMonth();
  const d = now.getUTCDate();
  const start = new Date(Date.UTC(y, mo, d, 0, 0)).toISOString();
  const end = new Date(Date.UTC(y, mo, d, 23, 59)).toISOString();
  return { start, end };
}

// ─── API ──────────────────────────────────────────────────────────────────────
// Debounce state for manual refresh — prevents double-clicks from firing two requests
let refreshPending = false;
function debouncedFetchFleet() {
  if (refreshPending) return;
  refreshPending = true;
  setTimeout(() => {
    refreshPending = false;
    fetchFleet();
  }, 500);
}

async function fetchFleet() {
  fleetLoading.value = true;
  fleetError.value = null;
  try {
    const result = await api.fleet.getFleetStatus({
      warn_seconds: WARN_SEC,
      offline_seconds: OFFLINE_SEC,
      limit: 1000
    });
    const raw = result as unknown as Record<string, unknown>;
    const details = raw?.details as Record<string, unknown> | undefined;
    const data = raw?.data as Record<string, unknown> | undefined;
    const payload = (details ?? data ?? raw) as unknown as FleetResponse;
    const g = (r: Record<string, unknown>, c: string, s: string) => r[c] ?? r[s];
    let list: Record<string, unknown>[] = [];
    if (Array.isArray(raw)) list = raw;
    else if (Array.isArray(details)) list = details as Record<string, unknown>[];
    else if (details && Array.isArray(details.items))
      list = details.items as Record<string, unknown>[];
    else if (Array.isArray(data)) list = data as Record<string, unknown>[];
    else if (data && Array.isArray(data.items)) list = data.items as Record<string, unknown>[];
    else list = (payload?.items ?? []) as unknown as Record<string, unknown>[];

    fleetItems.value = list.map(
      (r: Record<string, unknown>): FleetItem => ({
        status: (g(r, "status", "status") as "OK" | "WARN" | "OFFLINE") ?? "OFFLINE",
        hardwareId: (g(r, "hardwareId", "hardware_id") as string) ?? "",
        temperatureC: (g(r, "temperatureC", "temperature_c") as number | null) ?? null,
        humidityPct: (g(r, "humidityPct", "humidity_pct") as number | null) ?? null,
        lightLux: (g(r, "lightLux", "light_lux") as number | null) ?? null,
        batteryPct: (g(r, "batteryPct", "battery_pct") as number | null) ?? null,
        ts: (g(r, "ts", "ts") as string | null) ?? null,
        ageSeconds: (g(r, "ageSeconds", "age_seconds") as number | null) ?? null,
        truckId: (g(r, "truckId", "truck_id") as number | null) ?? null,
        truckName: (g(r, "truckName", "truck_name") as string | null) ?? null,
        plate: (g(r, "plate", "plate") as string | null) ?? null,
        sensorName: (g(r, "sensorName", "sensor_name") as string | null) ?? null
      })
    );

    fleetUpdated.value = formatTime(
      payload?.nowUtc ?? (raw?.nowUtc as string) ?? (details?.nowUtc as string) ?? null
    );

    if (fleetItems.value.length > 0) {
      const exists = fleetItems.value.some((i) => i.hardwareId === selectedDevice.value);
      if (!selectedDevice.value || !exists) setDevice(fleetItems.value[0].hardwareId);
    }
    lastUpdatedAt.value = Date.now();
    updateAgo();
  } catch (e: unknown) {
    fleetError.value = e instanceof Error ? e.message : String(e);
  } finally {
    fleetLoading.value = false;
  }
}

function startFleetTimer() {
  if (fleetTimer) clearInterval(fleetTimer);
  fleetTimer = setInterval(fetchFleet, FLEET_REFRESH_MS);
}

async function loadHistoryMeta(hw: string) {
  const result = await api.fleet.getHistoryMeta(hw);
  const meta = ((result as any).details ?? result) as Record<string, unknown>;
  const found = meta?.found ?? meta?.Found;
  const minTs = (meta?.minTs ?? meta?.min_ts) as string | null | undefined;
  const maxTs = (meta?.maxTs ?? meta?.max_ts) as string | null | undefined;
  if (found && minTs) metaMinTs.value = minTs;
  if (found && maxTs) metaMaxTs.value = maxTs;
  return meta;
}

async function queryHistory() {
  const hw = selectedDevice.value;
  if (!hw) return;

  const startIso = displayDateToStartIso(startUtc.value);
  const endIso = displayDateToEndIso(endUtc.value);
  if (!startIso || !endIso) {
    historyError.value = "Please set start and end date.";
    return;
  }
  if (new Date(endIso) < new Date(startIso)) {
    historyError.value = "End date must be on or after start date.";
    return;
  }

  const rangeHours = (new Date(endIso).getTime() - new Date(startIso).getTime()) / 3_600_000;
  const useAggregated = rangeHours > AGG_THRESHOLD_HOURS;

  historyLoading.value = true;
  historyError.value = null;
  try {
    // Always fetch raw rows so the history table always has data regardless of range.
    const rangeResult = await api.fleet.getHistoryRange(hw, startIso, endIso);
    const rangeRaw = rangeResult as unknown as Record<string, unknown>;
    const rangeDetails = rangeRaw?.details as Record<string, unknown> | undefined;
    const rangeData = rangeRaw?.data as Record<string, unknown> | undefined;
    const payload = rangeDetails ?? rangeData ?? rangeRaw;
    let rows: Record<string, unknown>[] = [];
    if (Array.isArray(payload)) rows = payload;
    else if (Array.isArray(payload?.rows)) rows = payload.rows as Record<string, unknown>[];
    else if (Array.isArray(rangeRaw?.rows)) rows = rangeRaw.rows as Record<string, unknown>[];
    const rawCount = (payload?.count ?? rangeRaw?.count ?? rows.length) as number;
    const g = (r: Record<string, unknown>, c: string, s: string) => r[c] ?? r[s];
    const tsOf = (r: Record<string, unknown>) => String(r.ts ?? r.timestamp ?? "");

    historyRows.value = rows
      .sort(
        (a: Record<string, unknown>, b: Record<string, unknown>) =>
          new Date(tsOf(b)).getTime() - new Date(tsOf(a)).getTime()
      )
      .map((r: Record<string, unknown>): HistoryRow => {
        const raw = (r.raw ?? r) as Record<string, unknown> | null;
        const vibrationG =
          raw != null
            ? ((raw["vibration_g"] ??
                raw["vibration"] ??
                raw["shake"] ??
                raw["acc_g"] ??
                raw["vibrationG"] ??
                null) as number | null)
            : null;
        const rssi = (g(r, "rssi", "signal_rssi") ??
          (raw != null ? raw["rssi"] ?? raw["signal_rssi"] ?? raw["signal"] ?? null : null)) as
          | number
          | null;

        return {
          ts: (g(r, "ts", "timestamp") as string) ?? "",
          temperatureC: (g(r, "temperatureC", "temperature_c") as number | null) ?? null,
          humidityPct: (g(r, "humidityPct", "humidity_pct") as number | null) ?? null,
          lightLux: (g(r, "lightLux", "light_lux") as number | null) ?? null,
          batteryPct: (g(r, "batteryPct", "battery_pct") as number | null) ?? null,
          vibrationG,
          rssi
        };
      });

    if (useAggregated) {
      // Also fetch hourly aggregates for chart rendering when range > 24 h.
      const aggResult = await api.fleet.getHistoryAggregated(hw, startIso, endIso, 60);
      const aggRaw = aggResult as unknown as Record<string, unknown>;
      const aggDetails = aggRaw?.details as Record<string, unknown> | undefined;
      const aggRows = ((aggDetails?.rows ?? aggRaw?.rows ?? []) as Record<string, unknown>[]);
      const aggCount = (aggDetails?.count ?? aggRaw?.count ?? aggRows.length) as number;

      aggregatedRows.value = aggRows.map((r): AggregatedRow => ({
        ts: String(r.ts ?? ""),
        temp_min: (r.temp_min as number | null) ?? null,
        temp_max: (r.temp_max as number | null) ?? null,
        temp_avg: (r.temp_avg as number | null) ?? null,
        hum_min: (r.hum_min as number | null) ?? null,
        hum_max: (r.hum_max as number | null) ?? null,
        hum_avg: (r.hum_avg as number | null) ?? null,
        light_min: (r.light_min as number | null) ?? null,
        light_max: (r.light_max as number | null) ?? null,
        light_avg: (r.light_avg as number | null) ?? null,
        batt_min: (r.batt_min as number | null) ?? null,
        batt_max: (r.batt_max as number | null) ?? null,
        batt_avg: (r.batt_avg as number | null) ?? null
      }));

      historyMeta.value = `Device: ${hw} | Points: ${rawCount} | Hourly buckets: ${aggCount} (range > 24 h)`;
    } else {
      aggregatedRows.value = [];
      historyMeta.value = `Device: ${hw} | Points: ${rawCount}`;
    }
  } catch (e: unknown) {
    historyError.value = e instanceof Error ? e.message : String(e);
  } finally {
    historyLoading.value = false;
  }
}

function startHistoryTimer() {
  if (historyTimer) clearInterval(historyTimer);
  historyTimer = setInterval(async () => {
    const hw = selectedDevice.value;
    if (!hw) return;
    await loadHistoryMeta(hw);
    await queryHistory();
  }, HISTORY_REFRESH_MS);
}

async function onDeviceSelected(hw: string | null) {
  if (!hw) return;
  historyRows.value = [];
  aggregatedRows.value = [];
  historyMeta.value = "";
  historyError.value = null;
  metaMinTs.value = null;
  metaMaxTs.value = null;
  startAlarmNotifier(hw);
  try {
    await loadHistoryMeta(hw);
    // Default to today's data — a focused, readable live view. The full backfilled
    // history (migration 0017) is one click away via "Full Range" below.
    const { start, end } = todayMytRange();
    startUtc.value = isoToDisplayDt(start);
    endUtc.value = isoToDisplayDt(end);
    await queryHistory();
  } catch (e: unknown) {
    historyError.value = e instanceof Error ? e.message : String(e);
  }
}

async function onFullRange() {
  if (metaMinTs.value && metaMaxTs.value) {
    startUtc.value = isoToDisplayDt(metaMinTs.value);
    endUtc.value = isoToDisplayDt(metaMaxTs.value);
  } else {
    const { start, end } = todayMytRange();
    startUtc.value = isoToDisplayDt(start);
    endUtc.value = isoToDisplayDt(end);
  }
  await queryHistory();
}

watch(selectedDevice, (hw) => {
  if (hw) onDeviceSelected(hw);
}, { immediate: true });

// ─── KPI banner (MES-style restyle pilot) — derived from fleetItems already in memory,
// no extra API calls. ─────────────────────────────────────────────────────────
const totalDevices = computed(() => fleetItems.value.length);
const activeCount = computed(() => fleetItems.value.filter((i) => i.status === "OK").length);
const warnCount = computed(() => fleetItems.value.filter((i) => i.status === "WARN").length);
const offlineCount = computed(() => fleetItems.value.filter((i) => i.status === "OFFLINE").length);

// Page Visibility API: pause auto-refresh when the tab is hidden to save requests,
// resume and immediately refresh when the user returns to the tab.
function onVisibilityChange() {
  if (document.hidden) {
    if (fleetTimer) { clearInterval(fleetTimer); fleetTimer = null; }
    if (historyTimer) { clearInterval(historyTimer); historyTimer = null; }
  } else {
    fetchFleet();
    void queryHistory();
    startFleetTimer();
    startHistoryTimer();
  }
}

onMounted(() => {
  fetchFleet();
  startFleetTimer();
  startHistoryTimer();
  agoTimer = setInterval(updateAgo, 15_000);
  document.addEventListener("visibilitychange", onVisibilityChange);
});

onUnmounted(() => {
  if (fleetTimer) clearInterval(fleetTimer);
  if (historyTimer) clearInterval(historyTimer);
  if (agoTimer) clearInterval(agoTimer);
  document.removeEventListener("visibilitychange", onVisibilityChange);
});
</script>

<template>
  <div>
    <HeaderAppV2
      titlePage="Fleet Dashboard"
      :breadcrumbs="['Monitoring', 'Fleet', 'Fleet Dashboard']"
    >
      <div class="row items-center gap-sm q-mt-xs">
        <q-btn
          no-caps
          color="primary"
          icon="fa-solid fa-plus"
          label="Add Device"
          @click="showPortal = true"
        />
      </div>
    </HeaderAppV2>

    <!-- KPI banner (MES-style pilot) -->
    <div :class="$style.kpiRow" class="q-mx-lg q-mt-lg">
      <div :class="[$style.kpiCard, $style.kpiBlue]">
        <div :class="$style.kpiLabel">Total Devices</div>
        <div :class="$style.kpiVal">{{ totalDevices }}</div>
      </div>
      <div :class="[$style.kpiCard, $style.kpiGreen]">
        <div :class="$style.kpiLabel">Active</div>
        <div :class="$style.kpiVal">{{ activeCount }}</div>
      </div>
      <div :class="[$style.kpiCard, $style.kpiAmber]">
        <div :class="$style.kpiLabel">Warning</div>
        <div :class="$style.kpiVal">{{ warnCount }}</div>
      </div>
      <div :class="[$style.kpiCard, $style.kpiRed]">
        <div :class="$style.kpiLabel">Offline</div>
        <div :class="$style.kpiVal">{{ offlineCount }}</div>
      </div>
    </div>

    <div class="app-container q-mx-lg q-mt-sm q-mb-sm">
      <div class="row bg-white">
        <!-- Fleet Status -->
        <div style="width: 100%">
          <FleetStatus
            :fleetItems="fleetItems"
            :fleetLoading="fleetLoading"
            :fleetError="fleetError"
            :fleetUpdated="fleetUpdated"
            @row-click="(hw) => setDevice(hw)"
          />
        </div>

        <!-- Device History section header -->
        <div :class="[$style.sectionHeader, 'headerColor text-weight-bold q-pa-sm q-px-md']">
          Device History
        </div>

        <!-- Filter controls -->
        <div :class="$style.searchContainer">
          <div class="q-pa-md">
            <div class="row q-col-gutter-sm items-end">
              <LabelApp label="Device" class="col col-12 col-sm-4 col-md-4">
                <DeviceDropdown
                  :model-value="selectedDevice ?? ''"
                  :devices="fleetItems"
                  @update:model-value="(v) => setDevice(v)"
                />
              </LabelApp>
              <LabelApp label="Start Date" class="col col-12 col-sm-4 col-md-4">
                <q-input
                  v-model="startUtc"
                  mask="##/##/####"
                  outlined
                  dense
                  placeholder="DD/MM/YYYY"
                >
                  <template #append>
                    <q-icon name="fa-solid fa-calendar-days" class="cursor-pointer">
                      <q-popup-proxy ref="startProxyRef" cover transition-show="scale" transition-hide="scale" @show="onDateShow" @hide="onDateHide">
                        <q-date
                          :model-value="startUtcDate"
                          mask="DD/MM/YYYY"
                          @update:model-value="(v: string) => setStartDate(v)"
                        >
                          <div class="row items-center justify-end">
                            <q-btn v-close-popup label="Close" color="primary" flat />
                          </div>
                        </q-date>
                      </q-popup-proxy>
                    </q-icon>
                  </template>
                </q-input>
              </LabelApp>
              <LabelApp label="End Date" class="col col-12 col-sm-4 col-md-4">
                <q-input v-model="endUtc" mask="##/##/####" outlined dense placeholder="DD/MM/YYYY">
                  <template #append>
                    <q-icon name="fa-solid fa-calendar-days" class="cursor-pointer">
                      <q-popup-proxy ref="endProxyRef" cover transition-show="scale" transition-hide="scale" @show="onDateShow" @hide="onDateHide">
                        <q-date
                          :model-value="endUtcDate"
                          mask="DD/MM/YYYY"
                          @update:model-value="(v: string) => setEndDate(v)"
                        >
                          <div class="row items-center justify-end">
                            <q-btn v-close-popup label="Close" color="primary" flat />
                          </div>
                        </q-date>
                      </q-popup-proxy>
                    </q-icon>
                  </template>
                </q-input>
              </LabelApp>
            </div>
            <div class="row q-gutter-sm q-mt-sm items-center">
              <q-btn
                color="primary"
                no-caps
                :loading="historyLoading"
                :disable="!selectedDevice"
                @click="queryHistory"
              >
                Query
              </q-btn>
              <q-btn
                outline
                no-caps
                color="white text-black"
                :disable="!selectedDevice"
                @click="onFullRange"
              >
                Full Range
              </q-btn>
              <span v-if="fullRangeDays > 1 && isShowingToday" class="text-caption text-grey-6">
                Showing today — {{ fullRangeDays }} days of history available
              </span>
              <span v-else-if="fullRangeDays > 1 && isShowingFullRange" class="text-caption text-grey-6">
                Showing full range ({{ fullRangeDays }} days)
              </span>
              <q-space />
              <div class="row items-center gap-sm">
                <q-btn
                  outline
                  no-caps
                  color="white text-black"
                  :loading="fleetLoading"
                  @click="debouncedFetchFleet"
                >
                  <q-icon class="q-pr-xs" size="13px" name="fa-solid fa-arrows-rotate" />
                  Refresh
                </q-btn>
                <span v-if="lastUpdatedAgo" class="text-caption text-grey-6">
                  Updated {{ lastUpdatedAgo }}
                </span>
              </div>
            </div>
            <div v-if="historyMeta" :class="[$style.historyMeta, 'text-caption q-mt-xs']">
              {{ historyMeta }}
            </div>
          </div>
        </div>

        <!-- Device History table + Charts side by side -->
        <div style="width: 100%">
          <div
            class="row items-stretch"
            style="height: clamp(1100px, calc(100vh - 100px), 1400px); overflow: hidden"
          >
            <div
              class="col"
              style="min-width: 0; display: flex; flex-direction: column; align-self: flex-start"
            >
              <FleetDeviceHistory
                :selectedDevice="selectedDevice"
                :historyError="historyError"
                :historyLoading="historyLoading"
                :historyRows="historyRows"
              />
            </div>
            <q-separator vertical />
            <FleetCharts :historyRows="historyRows" :aggregatedRows="aggregatedRows" />
          </div>
        </div>
      </div>
    </div>

    <FleetDevicePortal
      v-if="showPortal"
      @registered="(hw: string) => onDeviceRegistered(hw)"
      @close="showPortal = false"
    />
  </div>
</template>

<style lang="sass" module>
@import '../../../../style/_variables'

.searchContainer
  width: 100%
  background-color: $white
  margin-top: 10px

.sectionHeader
  width: 100%
  font-size: 12px
  font-weight: 700
  letter-spacing: 0.4px
  text-transform: uppercase
  color: #515151
  background-image: linear-gradient(0deg, $secondary-grey-2 0%, $white 100%)
  border-top: 1px solid $secondary-grey-2
  border-bottom: 1px solid #c3c6d4
  padding: 8px 12px !important

.historyMeta
  color: $secondary-grey-1

// ─── KPI banner (MES-style restyle pilot) ──────────────────────────────────────
.kpiRow
  display: flex
  gap: 1px
  background: $secondary-grey-2
  border: 1px solid $secondary-grey-2
  border-radius: 6px
  overflow: hidden

.kpiCard
  flex: 1
  background: $white
  padding: 9px 16px
  display: flex
  flex-direction: column
  gap: 3px
  border-left: 3px solid $status-info

.kpiLabel
  font-size: 11px
  color: #7f7f7f
  text-transform: uppercase
  letter-spacing: 0.5px

.kpiVal
  font-size: 22px
  font-weight: 800
  line-height: 1.1
  color: #515151

.kpiBlue
  border-left-color: $status-info

.kpiGreen
  border-left-color: $status-ok

.kpiAmber
  border-left-color: $status-warn

.kpiRed
  border-left-color: $status-alarm
</style>
