<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useRowHighlight } from "@/helpers/rowHighlight";
import api from "@/helpers/api";
import { type AlarmThresholds, defaultThresholds } from "../FleetRealTimeMonitoring/useRtmSettings";
import { connectivityBadge } from "./useFleetConnectivityStatus";

type FleetStatusType = "OK" | "WARN" | "OFFLINE";

export interface FleetItem {
  status: FleetStatusType;
  hardwareId: string;
  temperatureC: number | null;
  humidityPct: number | null;
  lightLux: number | null;
  batteryPct: number | null;
  ts: string | null;
  ageSeconds: number | null;
  truckId: number | null;
  truckName: string | null;
  plate: string | null;
  sensorName: string | null;
}

const props = defineProps<{
  fleetItems: FleetItem[];
  fleetLoading: boolean;
  fleetError: string | null;
  fleetUpdated: string | null;
}>();

const emit = defineEmits<{
  (e: "rowClick", hardwareId: string): void;
}>();

const { handleMouseOver, handleMouseLeave } = useRowHighlight();

const columns = [
  {
    name: "status",
    label: "Status",
    field: "status",
    align: "left" as const,
    sortable: true,
    style: "width: 100px"
  },
  {
    name: "hardwareId",
    label: "Hardware ID",
    field: "hardwareId",
    align: "left" as const,
    sortable: true,
    style: "width: 140px"
  },
  {
    name: "truckName",
    label: "Truck",
    field: "truckName",
    align: "left" as const,
    sortable: true,
    style: "width: 140px"
  },
  {
    name: "temperatureC",
    label: "Temp (°C)",
    field: "temperatureC",
    align: "right" as const,
    sortable: true,
    style: "width: 100px"
  },
  {
    name: "humidityPct",
    label: "Humidity (%)",
    field: "humidityPct",
    align: "right" as const,
    sortable: true,
    style: "width: 100px"
  },
  {
    name: "lightLux",
    label: "Light (lux)",
    field: "lightLux",
    align: "right" as const,
    sortable: true,
    style: "width: 90px"
  },
  {
    name: "batteryPct",
    label: "Battery (%)",
    field: "batteryPct",
    align: "right" as const,
    sortable: true,
    style: "width: 90px"
  },
  {
    name: "ageSeconds",
    label: "Age (min)",
    field: "ageSeconds",
    align: "right" as const,
    sortable: true,
    style: "width: 90px"
  }
];

const statusCounts = computed(() => ({
  ok: (props.fleetItems ?? []).filter((i) => i.status === "OK").length,
  warn: (props.fleetItems ?? []).filter((i) => i.status === "WARN").length,
  offline: (props.fleetItems ?? []).filter((i) => i.status === "OFFLINE").length
}));

// ─── Per-device threshold map ────────────────────────────────────────────────
const thresholdsMap = ref<Record<string, AlarmThresholds>>({});

function toNum(v: unknown): number | null {
  if (v === null || v === undefined || v === "") return null;
  const n = Number(v);
  return Number.isFinite(n) ? n : null;
}

async function fetchDeviceThresholds(hardwareId: string) {
  if (thresholdsMap.value[hardwareId]) return;
  try {
    const result = await api.fleet.getDeviceSettings(hardwareId);
    const raw = result as unknown as Record<string, unknown>;
    const details = raw?.details as Record<string, unknown> | undefined;
    const data = raw?.data as Record<string, unknown> | undefined;
    const row = (details?.row ?? details ?? data?.row ?? data ?? raw?.row) as
      | Record<string, unknown>
      | undefined;
    if (!row) {
      thresholdsMap.value[hardwareId] = { ...defaultThresholds };
      return;
    }
    const a = (row.alarm_json ?? row.alarmJson ?? {}) as Record<string, unknown>;
    thresholdsMap.value[hardwareId] = {
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
  } catch {
    thresholdsMap.value[hardwareId] = { ...defaultThresholds };
  }
}

watch(
  () => props.fleetItems,
  (items) => {
    (items ?? []).forEach((i) => fetchDeviceThresholds(i.hardwareId));
  },
  { immediate: true, deep: false }
);

function t(hardwareId: string): AlarmThresholds {
  return thresholdsMap.value[hardwareId] ?? defaultThresholds;
}

// ─── Helpers ─────────────────────────────────────────────────────────────────
function fmtOrDash(v: number | null | undefined, suffix = "", decimals = 1): string {
  return v != null ? `${Number(v).toFixed(decimals)}${suffix}` : "—";
}

function ageMinutes(seconds: number | null): string {
  if (seconds == null) return "—";
  return String(Math.round(seconds / 60));
}

// ─── Color class functions (Quasar design tokens) ────────────────────────────
function tempClass(v: number | null, hwId: string): string {
  if (v == null) return "text-grey-5";
  const th = t(hwId);
  const min = th.temp_min_c ?? null;
  const max = th.temp_max_c ?? null;
  const range = (max ?? 40) - (min ?? 0);
  const warnAt = (max ?? 40) - range * 0.2;
  if (max != null && v >= max) return "text-negative";
  if (min != null && v <= min) return "text-blue-8";
  if (max != null && v >= warnAt) return "text-orange-8";
  return "text-blue-6";
}

function tempAlert(v: number | null, hwId: string): boolean {
  if (v == null) return false;
  const th = t(hwId);
  const max = th.temp_max_c ?? null;
  const range = (max ?? 40) - (th.temp_min_c ?? 0);
  const warnAt = (max ?? 40) - range * 0.2;
  return max != null && v >= warnAt;
}

function humClass(v: number | null, hwId: string): string {
  if (v == null) return "text-grey-5";
  const th = t(hwId);
  if (th.humidity_max_pct != null && v > th.humidity_max_pct) return "text-orange-8";
  if (th.humidity_min_pct != null && v < th.humidity_min_pct) return "text-blue-8";
  return "";
}

function humAlert(v: number | null, hwId: string): boolean {
  if (v == null) return false;
  const th = t(hwId);
  return (
    (th.humidity_max_pct != null && v > th.humidity_max_pct) ||
    (th.humidity_min_pct != null && v < th.humidity_min_pct)
  );
}

function lightClass(v: number | null, hwId: string): string {
  if (v == null) return "text-grey-5";
  const th = t(hwId);
  if (
    (th.light_max_lux != null && v > th.light_max_lux) ||
    (th.light_min_lux != null && v < th.light_min_lux)
  )
    return "text-orange-8";
  return "";
}

function lightAlert(v: number | null, hwId: string): boolean {
  if (v == null) return false;
  const th = t(hwId);
  return (
    (th.light_max_lux != null && v > th.light_max_lux) ||
    (th.light_min_lux != null && v < th.light_min_lux)
  );
}

function batteryClass(v: number | null, hwId: string): string {
  if (v == null) return "text-grey-5";
  const th = t(hwId);
  if (th.battery_min_pct != null && v < th.battery_min_pct) return "text-negative";
  return "";
}

function batteryAlert(v: number | null, hwId: string): boolean {
  if (v == null) return false;
  const th = t(hwId);
  return th.battery_min_pct != null && v < th.battery_min_pct;
}

</script>

<template>
  <div>
    <!-- Error banner -->
    <q-banner v-if="fleetError" class="q-mx-md q-mt-sm bg-red-1 text-red-9 radius-5" dense rounded>
      <template #avatar>
        <q-icon name="fa-solid fa-circle-exclamation" color="negative" />
      </template>
      {{ fleetError }}
    </q-banner>

    <!-- Fleet table — virtual-scroll caps DOM nodes for large fleets (100+ devices) -->
    <q-table
      :rows="fleetItems ?? []"
      :columns="columns"
      row-key="hardwareId"
      :loading="fleetLoading"
      flat
      separator="cell"
      style="max-height: 400px; table-layout: fixed; width: 100%; font-variant-numeric: tabular-nums; overflow-y: auto"
      :rows-per-page-options="[0]"
      hide-bottom
      :class="[$style.clickableRows, $style.styledTable]"
      @row-click="(_, row) => emit('rowClick', row.hardwareId)"
      @mouseover="handleMouseOver"
      @mouseleave="handleMouseLeave"
    >
      <!-- Status -->
      <template #body-cell-status="slotProps">
        <q-td auto-width :props="slotProps">
          <q-badge
            :color="connectivityBadge(slotProps.row.status).color"
            :text-color="connectivityBadge(slotProps.row.status).textColor"
            class="q-px-sm"
          >
            <q-icon :name="connectivityBadge(slotProps.row.status).icon" size="11px" class="q-mr-xs" />
            {{ connectivityBadge(slotProps.row.status).label }}
          </q-badge>
        </q-td>
      </template>

      <!-- Hardware ID -->
      <template #body-cell-hardwareId="slotProps">
        <q-td auto-width :props="slotProps">
          {{ slotProps.row.hardwareId }}
        </q-td>
      </template>

      <!-- Truck -->
      <template #body-cell-truckName="slotProps">
        <q-td auto-width :props="slotProps">
          <span v-if="slotProps.row.truckName">{{ slotProps.row.truckName }}</span>
          <span v-else class="text-grey-5 text-italic">Unassigned</span>
        </q-td>
      </template>

      <!-- Temperature -->
      <template #body-cell-temperatureC="slotProps">
        <q-td auto-width :props="slotProps" class="text-right">
          <span
            :class="[
              tempClass(slotProps.row.temperatureC, slotProps.row.hardwareId),
              'text-weight-bold'
            ]"
          >
            {{ fmtOrDash(slotProps.row.temperatureC, "°C", 1) }}
            <q-icon
              v-if="tempAlert(slotProps.row.temperatureC, slotProps.row.hardwareId)"
              name="fa-solid fa-triangle-exclamation"
              size="11px"
              class="q-ml-xs"
            />
          </span>
        </q-td>
      </template>

      <!-- Humidity -->
      <template #body-cell-humidityPct="slotProps">
        <q-td auto-width :props="slotProps" class="text-right">
          <span
            :class="[
              humClass(slotProps.row.humidityPct, slotProps.row.hardwareId),
              'text-weight-bold'
            ]"
          >
            {{ fmtOrDash(slotProps.row.humidityPct, "%", 1) }}
            <q-icon
              v-if="humAlert(slotProps.row.humidityPct, slotProps.row.hardwareId)"
              name="fa-solid fa-triangle-exclamation"
              size="11px"
              class="q-ml-xs"
            />
          </span>
        </q-td>
      </template>

      <!-- Light -->
      <template #body-cell-lightLux="slotProps">
        <q-td auto-width :props="slotProps" class="text-right">
          <span
            :class="[
              lightClass(slotProps.row.lightLux, slotProps.row.hardwareId),
              'text-weight-bold'
            ]"
          >
            {{ fmtOrDash(slotProps.row.lightLux, "", 0) }}
            <q-icon
              v-if="lightAlert(slotProps.row.lightLux, slotProps.row.hardwareId)"
              name="fa-solid fa-triangle-exclamation"
              size="11px"
              class="q-ml-xs"
            />
          </span>
        </q-td>
      </template>

      <!-- Battery -->
      <template #body-cell-batteryPct="slotProps">
        <q-td auto-width :props="slotProps" class="text-right">
          <span
            :class="[
              batteryClass(slotProps.row.batteryPct, slotProps.row.hardwareId),
              'text-weight-bold'
            ]"
          >
            {{ fmtOrDash(slotProps.row.batteryPct, "%", 0) }}
            <q-icon
              v-if="batteryAlert(slotProps.row.batteryPct, slotProps.row.hardwareId)"
              name="fa-solid fa-triangle-exclamation"
              size="11px"
              class="q-ml-xs"
            />
          </span>
        </q-td>
      </template>

      <template #body-cell-ageSeconds="slotProps">
        <q-td auto-width :props="slotProps" class="text-right">
          {{ ageMinutes(slotProps.row.ageSeconds) }}
        </q-td>
      </template>

      <template #no-data>
        <div class="full-width text-center q-pa-lg text-grey-5 text-body1">
          <q-icon name="fa-solid fa-satellite-dish" size="40px" class="q-mb-sm" />
          <br />
          No fleet data available
        </div>
      </template>

      <template #bottom-row>
        <tr>
          <td
            :colspan="columns.length"
            class="q-px-md q-py-xs"
            :class="$style.summaryRow"
          >
            <div class="row items-center gap-sm">
              <q-badge
                :color="connectivityBadge('OK').color"
                :text-color="connectivityBadge('OK').textColor"
                class="q-px-sm text-caption"
              >
                <q-icon :name="connectivityBadge('OK').icon" size="11px" class="q-mr-xs" />
                {{ connectivityBadge("OK").label }}: {{ statusCounts.ok }}
              </q-badge>
              <q-badge
                :color="connectivityBadge('WARN').color"
                :text-color="connectivityBadge('WARN').textColor"
                class="q-px-sm text-caption"
              >
                <q-icon :name="connectivityBadge('WARN').icon" size="11px" class="q-mr-xs" />
                {{ connectivityBadge("WARN").label }}: {{ statusCounts.warn }}
              </q-badge>
              <q-badge
                :color="connectivityBadge('OFFLINE').color"
                :text-color="connectivityBadge('OFFLINE').textColor"
                class="q-px-sm text-caption"
              >
                <q-icon :name="connectivityBadge('OFFLINE').icon" size="11px" class="q-mr-xs" />
                {{ connectivityBadge("OFFLINE").label }}: {{ statusCounts.offline }}
              </q-badge>
              <q-space />
              <span v-if="fleetUpdated" class="text-caption text-grey-6">
                Updated: {{ fleetUpdated }}
              </span>
            </div>
          </td>
        </tr>
      </template>
    </q-table>
  </div>
</template>

<style lang="sass" module>
@import '../../../../style/_variables'

.clickableRows
  :global(tbody tr)
    cursor: pointer
  :global(tbody tr:hover td)
    background: $primary-grey-1 !important

.styledTable
  :global(thead tr th)
    font-weight: 700 !important
    font-size: 14px !important
    color: $primary-black !important
    text-transform: capitalize !important
    background-image: linear-gradient(0deg, $secondary-grey-2 0%, $white 100%) !important
    box-sizing: border-box !important
    border-top: 1px solid $secondary-grey-2 !important
  :global(tbody tr td)
    padding: 8px !important
    font-size: 14px !important
    color: $primary-black !important

.summaryRow
  border-top: 1px solid $secondary-grey-2
</style>
