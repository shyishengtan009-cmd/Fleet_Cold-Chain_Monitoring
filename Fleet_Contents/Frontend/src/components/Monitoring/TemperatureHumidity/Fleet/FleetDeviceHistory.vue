<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useRowHighlight } from "@/helpers/rowHighlight";
import { useRtmSettings } from "@/components/Monitoring/TemperatureHumidity/FleetRealTimeMonitoring/useRtmSettings";

export interface HistoryRow {
  ts: string;
  temperatureC: number | null;
  humidityPct: number | null;
  lightLux: number | null;
  batteryPct: number | null;
  vibrationG: number | null;
  rssi: number | null;
}

const props = defineProps<{
  selectedDevice: string | null;
  historyError: string | null;
  historyLoading: boolean;
  historyRows: HistoryRow[];
}>();

const { handleMouseOver, handleMouseLeave } = useRowHighlight();
const { thresholds, fetchSettings } = useRtmSettings();
watch(
  () => props.selectedDevice,
  (hw) => {
    if (hw) fetchSettings(hw);
  },
  { immediate: true }
);

const tableColumns = [
  {
    name: "ts",
    label: "Timestamp",
    field: "ts",
    align: "left" as const,
    sortable: true,
    style: "width: 28%"
  },
  {
    name: "temperatureC",
    label: "Temp (°C)",
    field: "temperatureC",
    align: "right" as const,
    sortable: true,
    style: "width: 12%"
  },
  {
    name: "humidityPct",
    label: "Hum (%)",
    field: "humidityPct",
    align: "right" as const,
    sortable: true,
    style: "width: 12%"
  },
  {
    name: "lightLux",
    label: "Light",
    field: "lightLux",
    align: "right" as const,
    sortable: true,
    style: "width: 10%"
  },
  {
    name: "batteryPct",
    label: "Bat (%)",
    field: "batteryPct",
    align: "right" as const,
    sortable: true,
    style: "width: 12%"
  },
  {
    name: "vibrationG",
    label: "Vib (g)",
    field: "vibrationG",
    align: "right" as const,
    sortable: true,
    style: "width: 13%"
  },
  {
    name: "rssi",
    label: "RSSI",
    field: "rssi",
    align: "right" as const,
    sortable: true,
    style: "width: 13%"
  }
];

function fmt(v: number | null | undefined, decimals = 1): string {
  return v != null ? Number(v).toFixed(decimals) : "—";
}

function fmtTs(iso: string): string {
  try {
    return new Date(iso).toLocaleString("en-GB", {
      timeZone: "Asia/Kuala_Lumpur",
      day: "2-digit",
      month: "short",
      year: "numeric",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
      hour12: false
    });
  } catch {
    return iso;
  }
}

const pagination = ref({ sortBy: "ts", descending: true, rowsPerPage: 21, page: 1 });
const totalPages = computed(() =>
  Math.max(1, Math.ceil((props.historyRows?.length ?? 0) / pagination.value.rowsPerPage))
);

// Reset to page 1 whenever the result set changes — otherwise a stale page index
// from a previous (larger) query can point past the end of a smaller new result,
// making q-table render an empty page even though rows were returned.
watch(
  () => props.historyRows,
  () => {
    pagination.value.page = 1;
  }
);

function tempClass(v: number | null): string {
  if (v == null) return "text-grey-5";
  const th = thresholds.value;
  const min = th.temp_min_c ?? null;
  const max = th.temp_max_c ?? null;
  const range = (max ?? 40) - (min ?? 0);
  const warnAt = (max ?? 40) - range * 0.2;
  if (max != null && v >= max) return "text-negative";
  if (min != null && v <= min) return "text-blue-8";
  if (max != null && v >= warnAt) return "text-orange-8";
  return "text-blue-6";
}

function tempAlert(v: number | null): boolean {
  if (v == null) return false;
  const th = thresholds.value;
  const max = th.temp_max_c ?? null;
  const range = (max ?? 40) - (th.temp_min_c ?? 0);
  const warnAt = (max ?? 40) - range * 0.2;
  return max != null && v >= warnAt;
}

function humClass(v: number | null): string {
  if (v == null) return "text-grey-5";
  const th = thresholds.value;
  if (th.humidity_max_pct != null && v > th.humidity_max_pct) return "text-orange-8";
  if (th.humidity_min_pct != null && v < th.humidity_min_pct) return "text-blue-8";
  return "";
}

function humAlert(v: number | null): boolean {
  if (v == null) return false;
  const th = thresholds.value;
  return (
    (th.humidity_max_pct != null && v > th.humidity_max_pct) ||
    (th.humidity_min_pct != null && v < th.humidity_min_pct)
  );
}

function lightClass(v: number | null): string {
  if (v == null) return "text-grey-5";
  const th = thresholds.value;
  if (
    (th.light_max_lux != null && v > th.light_max_lux) ||
    (th.light_min_lux != null && v < th.light_min_lux)
  )
    return "text-orange-8";
  return "";
}

function lightAlert(v: number | null): boolean {
  if (v == null) return false;
  const th = thresholds.value;
  return (
    (th.light_max_lux != null && v > th.light_max_lux) ||
    (th.light_min_lux != null && v < th.light_min_lux)
  );
}

function batteryClass(v: number | null): string {
  if (v == null) return "text-grey-5";
  const th = thresholds.value;
  if (th.battery_min_pct != null && v < th.battery_min_pct) return "text-negative";
  return "";
}

function batteryAlert(v: number | null): boolean {
  if (v == null) return false;
  const th = thresholds.value;
  return th.battery_min_pct != null && v < th.battery_min_pct;
}
</script>

<template>
  <div style="width: 100%; display: flex; flex-direction: column">
    <!-- Error banner -->
    <q-banner
      v-if="historyError"
      class="q-mx-md q-mt-sm bg-red-1 text-red-9 radius-5"
      dense
      rounded
    >
      <template #avatar>
        <q-icon name="fa-solid fa-circle-exclamation" color="negative" />
      </template>
      {{ historyError }}
    </q-banner>

    <!-- Data table -->
    <div style="overflow-x: auto">
      <q-table
        v-if="historyRows?.length"
        v-model:pagination="pagination"
        :rows="historyRows ?? []"
        :columns="tableColumns"
        row-key="ts"
        flat
        separator="cell"
        hide-bottom
        style="font-variant-numeric: tabular-nums; width: 100%; min-width: 500px"
        :rows-per-page-options="[]"
        :class="$style.styledTable"
        @mouseover="handleMouseOver"
        @mouseleave="handleMouseLeave"
      >
        <template #body-cell-ts="slotProps">
          <q-td :props="slotProps" style="white-space: nowrap; font-size: 12px">
            {{ fmtTs(slotProps.row.ts) }}
          </q-td>
        </template>

        <template #body-cell-temperatureC="slotProps">
          <q-td :props="slotProps" class="text-right">
            <span :class="[tempClass(slotProps.row.temperatureC), 'text-weight-bold']">
              {{ fmt(slotProps.row.temperatureC, 1) }}
              <q-icon
                v-if="tempAlert(slotProps.row.temperatureC)"
                name="fa-solid fa-triangle-exclamation"
                size="11px"
                class="q-ml-xs"
              />
            </span>
          </q-td>
        </template>

        <template #body-cell-humidityPct="slotProps">
          <q-td :props="slotProps" class="text-right">
            <span :class="[humClass(slotProps.row.humidityPct), 'text-weight-bold']">
              {{ fmt(slotProps.row.humidityPct, 1) }}
              <q-icon
                v-if="humAlert(slotProps.row.humidityPct)"
                name="fa-solid fa-triangle-exclamation"
                size="11px"
                class="q-ml-xs"
              />
            </span>
          </q-td>
        </template>

        <template #body-cell-lightLux="slotProps">
          <q-td :props="slotProps" class="text-right">
            <span :class="[lightClass(slotProps.row.lightLux), 'text-weight-bold']">
              {{ fmt(slotProps.row.lightLux, 0) }}
              <q-icon
                v-if="lightAlert(slotProps.row.lightLux)"
                name="fa-solid fa-triangle-exclamation"
                size="11px"
                class="q-ml-xs"
              />
            </span>
          </q-td>
        </template>

        <template #body-cell-batteryPct="slotProps">
          <q-td :props="slotProps" class="text-right">
            <span :class="[batteryClass(slotProps.row.batteryPct), 'text-weight-bold']">
              {{ fmt(slotProps.row.batteryPct, 0) }}
              <q-icon
                v-if="batteryAlert(slotProps.row.batteryPct)"
                name="fa-solid fa-triangle-exclamation"
                size="11px"
                class="q-ml-xs"
              />
            </span>
          </q-td>
        </template>

        <template #body-cell-vibrationG="slotProps">
          <q-td :props="slotProps" class="text-right">
            {{ slotProps.row.vibrationG != null ? fmt(slotProps.row.vibrationG, 3) : "—" }}
          </q-td>
        </template>

        <template #body-cell-rssi="slotProps">
          <q-td :props="slotProps" class="text-right">
            {{ slotProps.row.rssi != null ? slotProps.row.rssi : "—" }}
          </q-td>
        </template>

        <template #no-data>
          <div class="full-width text-center q-pa-md text-grey-5">No data</div>
        </template>
      </q-table>
    </div>

    <!-- Pagination always visible outside scroll area -->
    <div
      v-if="historyRows?.length"
      class="row items-center justify-end q-px-sm q-py-xs bg-white"
      :class="$style.paginationBar"
      style="gap: 4px; flex-shrink: 0"
    >
      <span class="text-caption text-grey-6">Page {{ pagination.page }} of {{ totalPages }}</span>
      <q-btn
        flat
        round
        dense
        size="sm"
        icon="fa-solid fa-chevron-left"
        :disable="pagination.page <= 1"
        @click="pagination.page--"
      />
      <q-btn
        flat
        round
        dense
        size="sm"
        icon="fa-solid fa-chevron-right"
        :disable="pagination.page >= totalPages"
        @click="pagination.page++"
      />
    </div>
  </div>
</template>

<style lang="sass" module>
@import '../../../../style/_variables'

.styledTable
  :global(thead tr th)
    font-weight: 700 !important
    font-size: 11px !important
    letter-spacing: 0.3px !important
    color: #515151 !important
    text-transform: uppercase !important
    background-image: linear-gradient(0deg, $secondary-grey-2 0%, $white 100%) !important
    box-sizing: border-box !important
    padding: 7px 10px !important
    border-bottom: 2px solid $secondary-grey-2 !important
  :global(tbody tr td)
    padding: 7px 10px !important
    font-size: 12px !important
    color: #515151 !important

.paginationBar
  border-top: 1px solid $secondary-grey-2
</style>
