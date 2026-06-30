<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from "vue";
import api from "@/helpers/api";
import HeaderAppV2 from "@/components/common/HeaderAppV2.vue";
import AlertAlarmTableEnhanced from "./AlertAlarmTableEnhanced.vue";
import LabelApp from "@/components/common/LabelApp.vue";
import DeviceDropdown from "../DeviceDropdown.vue";
import InputDateApp from "@/components/common/InputDateApp.vue";
import { useFleetDeviceSelection } from "../Fleet/useFleetDeviceSelection";

const { selectedHardwareId, setDevice } = useFleetDeviceSelection();

type SimpleDevice = {
  hardwareId: string;
  label: string;
  truckName?: string;
  status: "OK" | "WARN" | "OFFLINE";
};

type SimpleAlarm = {
  id: string;
  hardwareId: string;
  label: string;
  truckName: string;
  field: string;
  value: number;
  thresholdMin: number | null;
  thresholdMax: number | null;
  level: "ALARM" | "WARN" | "OK";
  ts: string;
  message?: string;
};

const loading = ref(false);
const error = ref<string | null>(null);
const lastUpdated = ref<string | null>(null);

const devices = ref<SimpleDevice[]>([]);
const alarms = ref<SimpleAlarm[]>([]);
const selectedDate = ref<string>("");
const thresholds = ref({
  temp_min_c: null as number | null,
  temp_max_c: null as number | null,
  humidity_min_pct: null as number | null,
  humidity_max_pct: null as number | null,
  light_min_lux: null as number | null,
  light_max_lux: null as number | null,
  vibration_g: null as number | null,
  battery_min_pct: null as number | null
});

const hasSelection = computed(() => !!selectedHardwareId.value);

const selectedDeviceInfo = computed(
  () => devices.value.find((d) => d.hardwareId === selectedHardwareId.value) ?? null
);

let refreshTimer: number | undefined;
let autoRefreshInFlight = false;

function startAutoRefresh() {
  if (refreshTimer !== undefined) window.clearInterval(refreshTimer);
  refreshTimer = window.setInterval(() => {
    void autoRefresh();
  }, 30 * 1000);
}

function handleAlertVisibility() {
  if (document.visibilityState === "hidden") {
    if (refreshTimer !== undefined) {
      window.clearInterval(refreshTimer);
      refreshTimer = undefined;
    }
    return;
  }
  void autoRefresh();
  startAutoRefresh();
}

function toNum(v: unknown): number | null {
  if (v === null || v === undefined || v === "") return null;
  const n = Number(v);
  return Number.isFinite(n) ? n : null;
}

function classifyLevel(
  value: number,
  min: number | null,
  max: number | null,
  level: "ALARM" | "WARN" = "ALARM"
): "ALARM" | "WARN" | null {
  if (max !== null && value >= max) return level;
  if (min !== null && value <= min) return level;
  return null;
}

function todayDisplay(): string {
  const d = new Date();
  const dd = String(d.getDate()).padStart(2, "0");
  const mm = String(d.getMonth() + 1).padStart(2, "0");
  return `${dd}/${mm}/${d.getFullYear()}`;
}

// Defaults the date filter to the device's own latest known reading date.
// A live device's latest reading IS today, so this naturally shows "today" for
// active devices while still showing a stale device's real last-known date
// instead of silently querying an empty "today" with no data.
async function dateForDevice(hw: string): Promise<string> {
  try {
    const meta = (await api.fleet.getHistoryMeta(hw)) as unknown as Record<string, unknown>;
    const details = (meta?.details ?? meta) as Record<string, unknown>;
    const found = details?.found ?? details?.Found;
    const maxTs = (details?.maxTs ?? details?.max_ts) as string | null | undefined;
    if (found && maxTs) {
      const d = new Date(maxTs);
      if (!isNaN(d.getTime())) {
        const dd = String(d.getUTCDate()).padStart(2, "0");
        const mm = String(d.getUTCMonth() + 1).padStart(2, "0");
        return `${dd}/${mm}/${d.getUTCFullYear()}`;
      }
    }
  } catch {
    // fall through to today
  }
  return todayDisplay();
}

async function loadDevices() {
  try {
    loading.value = true;
    error.value = null;

    // 2 parallel calls instead of N+1: device list + fleet status for truck names.
    // getFleetStatus already returns truckName for every device in one round-trip.
    const [devRes, statusRes] = await Promise.all([
      api.fleet.getDevices() as Promise<any>,
      api.fleet.getFleetStatus({ limit: 1000 }) as Promise<any>
    ]);

    const list = (devRes?.details?.rows ?? devRes?.details ?? devRes?.data ?? []) as any[];

    const statusRaw = statusRes as any;
    const statusItems: any[] = Array.isArray(statusRaw)
      ? statusRaw
      : statusRaw?.details?.items ??
        statusRaw?.details ??
        statusRaw?.data?.items ??
        statusRaw?.data ??
        statusRaw?.items ??
        [];

    const truckNameMap = new Map<string, string>();
    const statusMap = new Map<string, "OK" | "WARN" | "OFFLINE">();
    for (const item of statusItems) {
      const hw = item.hardwareId || item.hardware_id;
      const name = item.truckName || item.truck_name;
      const st = item.status as "OK" | "WARN" | "OFFLINE" | undefined;
      if (hw && name) truckNameMap.set(hw, name);
      if (hw && st) statusMap.set(hw, st);
    }

    // Reverse so earliest registered device appears first (leftmost tab)
    devices.value = [...list].reverse().map((d: any) => {
      const hw = d.hardware_id || d.hardwareId;
      const truckName = truckNameMap.get(hw);
      return {
        hardwareId: hw,
        label: truckName ? `${truckName} — ${hw}` : hw,
        truckName: truckName || undefined,
        status: statusMap.get(hw) ?? "OFFLINE"
      };
    });

    if (devices.value.length > 0) {
      const exists = devices.value.some((d) => d.hardwareId === selectedHardwareId.value);
      if (!selectedHardwareId.value || !exists) setDevice(devices.value[0].hardwareId);
    }
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e);
  } finally {
    loading.value = false;
  }
}

async function loadAlarms() {
  if (!selectedHardwareId.value) return;
  loading.value = true;
  error.value = null;
  try {
    let apiDate = "";
    if (selectedDate.value) {
      const parts = selectedDate.value.split("/");
      if (parts.length === 3) {
        apiDate = `${parts[2]}-${parts[1].padStart(2, "0")}-${parts[0].padStart(2, "0")}`;
      }
    }
    if (!apiDate) {
      const today = new Date();
      apiDate = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}-${String(today.getDate()).padStart(2, "0")}`;
    }

    const res = await (api.fleet.getSensorReadingsByDate(selectedHardwareId.value, apiDate, 2000) as Promise<any>);

    const list = (res?.details?.rows ?? res?.details ?? res?.data ?? []) as any[];

    const deviceTruckName =
      devices.value.find((d) => d.hardwareId === selectedHardwareId.value)?.truckName || "";

    const t = thresholds.value;
    const mappedAlarms: SimpleAlarm[] = [];
    list.forEach((row: any) => {
      const base = {
        hardwareId: row.hardware_id,
        label: row.hardware_id,
        truckName: deviceTruckName,
        ts: row.ts,
        thresholdMin: null,
        thresholdMax: null
      };

      // Push all 4 fields for every reading so the table always shows data.
      // Level is ALARM/WARN if threshold is breached, OK otherwise.
      mappedAlarms.push({
        ...base,
        id: `${row.id}-temp`,
        field: "temperature",
        value: row.temperature ?? 0,
        level: classifyLevel(row.temperature ?? 0, t.temp_min_c, t.temp_max_c, "ALARM") ?? "OK"
      });
      mappedAlarms.push({
        ...base,
        id: `${row.id}-humidity`,
        field: "humidity",
        value: row.humidity ?? 0,
        level:
          classifyLevel(row.humidity ?? 0, t.humidity_min_pct, t.humidity_max_pct, "WARN") ?? "OK"
      });
      mappedAlarms.push({
        ...base,
        id: `${row.id}-light`,
        field: "light",
        value: row.light ?? 0,
        level: classifyLevel(row.light ?? 0, t.light_min_lux, t.light_max_lux, "WARN") ?? "OK"
      });
      mappedAlarms.push({
        ...base,
        id: `${row.id}-battery`,
        field: "battery",
        value: row.battery ?? 0,
        level: classifyLevel(row.battery ?? 0, t.battery_min_pct, null, "ALARM") ?? "OK"
      });
    });

    alarms.value = mappedAlarms;

    lastUpdated.value = new Date().toLocaleTimeString("en-GB");
  } catch (e: any) {
    error.value = e?.message ?? "Failed to load alerts.";
  } finally {
    loading.value = false;
  }
}

async function loadDeviceSettings() {
  if (!selectedHardwareId.value) return;
  try {
    const result = (await api.fleet.getDeviceSettings(selectedHardwareId.value)) as unknown as any;
    const details = (result?.details ?? result?.data) as Record<string, unknown> | undefined;
    const row = (details?.row ?? details ?? result?.row) as Record<string, unknown> | undefined;
    if (!row || typeof row !== "object") return;

    const a = (row.alarm_json ?? (row as any).alarmJson ?? {}) as Record<string, unknown>;
    thresholds.value = {
      temp_min_c: toNum(a.temp_min_c),
      temp_max_c: toNum(a.temp_max_c),
      humidity_min_pct: toNum(a.humidity_min_pct),
      humidity_max_pct: toNum(a.humidity_max_pct),
      light_min_lux: toNum(a.light_min_lux),
      light_max_lux: toNum(a.light_max_lux),
      vibration_g: toNum(a.vibration_g),
      battery_min_pct: toNum(a.battery_min_pct)
    };
  } catch {
    // settings may not be configured
  }
}

async function onSelectHardware(hw: string) {
  setDevice(hw);
  selectedDate.value = await dateForDevice(hw);
  await loadDeviceSettings();
  await loadAlarms();
}

function onSearch() {
  void loadAlarms();
}

function onResetFilters() {
  selectedDate.value = todayDisplay();
  void loadAlarms();
}

async function onRefresh() {
  // Settings only reload on manual refresh or device switch — not every auto-tick.
  // Thresholds don't change often; calling settings every 30s is wasteful.
  await loadDeviceSettings();
  await loadAlarms();
}

async function autoRefresh() {
  if (autoRefreshInFlight) return;
  autoRefreshInFlight = true;
  try {
    await loadAlarms();
  } finally {
    autoRefreshInFlight = false;
  }
}

watch(selectedDate, () => {
  if (selectedHardwareId.value) void loadAlarms();
});

onMounted(async () => {
  await loadDevices();
  selectedDate.value = selectedHardwareId.value
    ? await dateForDevice(selectedHardwareId.value)
    : todayDisplay();
  await loadDeviceSettings(); // must load thresholds before classifying alarms
  await loadAlarms();
  startAutoRefresh();
  document.addEventListener("visibilitychange", handleAlertVisibility);
});

onUnmounted(() => {
  if (refreshTimer !== undefined) {
    window.clearInterval(refreshTimer);
    refreshTimer = undefined;
  }
  document.removeEventListener("visibilitychange", handleAlertVisibility);
});
</script>

<template>
  <div>
    <HeaderAppV2 titlePage="Cold Truck Alerts" :breadcrumbs="['Monitoring', 'Cold Truck Alerts']" />

    <div class="app-container q-mx-lg q-mt-lg q-mb-sm">
      <div class="row bg-white">
        <div :class="[$style.sectionHeader, 'headerColor text-weight-bold q-pa-sm q-px-md']">
          Alert Search
        </div>

        <!-- Filter Section -->
        <div :class="$style.searchContainer">
          <!-- Error state -->
          <q-banner v-if="error" class="bg-red-1 text-negative q-mx-md q-mt-md" rounded dense>
            {{ error }}
          </q-banner>

          <!-- Date filter + actions -->
          <div class="q-pa-md">
            <div class="row q-col-gutter-sm items-end">
              <LabelApp label="Device" class="col col-12 col-sm-4 col-md-4">
                <DeviceDropdown
                  :model-value="selectedHardwareId ?? ''"
                  :devices="devices as any"
                  @update:model-value="onSelectHardware"
                />
              </LabelApp>
              <InputDateApp
                class="col col-12 col-sm-4 col-md-4"
                label="Select Date"
                :date="selectedDate"
                @on-change="(date: string) => (selectedDate = date)"
              />
            </div>
            <div class="row q-gutter-sm q-mt-sm items-center">
              <q-btn color="primary" no-caps label="Search" @click="onSearch" />
              <q-btn outline no-caps color="white text-black" @click="onResetFilters">Reset</q-btn>
              <q-space />
              <span v-if="lastUpdated" class="text-caption text-grey-7">
                Updated: {{ lastUpdated }}
              </span>
              <q-btn outline no-caps color="white text-black" :loading="loading" @click="onRefresh">
                <q-icon class="q-pr-xs" size="13px" name="fa-solid fa-arrows-rotate" />
                Refresh
              </q-btn>
            </div>
          </div>
        </div>

        <!-- Empty selection prompt -->
        <q-card
          v-if="!hasSelection && !loading"
          flat
          class="text-center text-grey-5 q-pa-xl"
          style="width: 100%"
        >
          <div>Select a Fleet device to view alerts.</div>
        </q-card>

        <!-- Sensor Readings Table -->
        <div v-if="hasSelection" class="full-width">
          <AlertAlarmTableEnhanced
            :alarms="alarms"
            :thresholds="thresholds"
            :loading="loading"
            :truck-name="selectedDeviceInfo?.truckName || ''"
            :device-status="selectedDeviceInfo?.status ?? null"
          />
        </div>
      </div>
    </div>
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
</style>
