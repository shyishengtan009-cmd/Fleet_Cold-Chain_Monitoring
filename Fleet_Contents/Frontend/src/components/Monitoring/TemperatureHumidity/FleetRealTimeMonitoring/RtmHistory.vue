<script setup lang="ts">
import type { AlarmThresholds } from "./useRtmSettings";
import { useRowHighlight } from "@/helpers/rowHighlight";
import { STATUS_ALARM, STATUS_INFO, STATUS_WARN } from "@/style/statusColors";

const props = defineProps<{
  historyLoading: boolean;
  historyRows: any[];
  thresholds: AlarmThresholds;
  fmt: (...args: any[]) => string;
  fmtTs: (...args: any[]) => string;
}>();

const { handleMouseOver, handleMouseLeave } = useRowHighlight();

function tempColor(v: number | null): string {
  if (v == null) return "#888";
  const { temp_min_c, temp_max_c } = props.thresholds;
  const range = (temp_max_c ?? 40) - (temp_min_c ?? 0);
  const warnAt = (temp_max_c ?? 40) - range * 0.2;
  if (temp_max_c != null && v >= temp_max_c) return STATUS_ALARM;
  if (temp_min_c != null && v <= temp_min_c) return STATUS_INFO;
  if (temp_max_c != null && v >= warnAt) return STATUS_WARN;
  return "#1976d2";
}
</script>

<template>
  <div class="q-pa-none">
    <div v-if="historyLoading" class="text-center q-pa-lg">
      <q-spinner color="primary" size="32px" />
    </div>
    <q-table
      v-else
      :rows="[...historyRows].reverse()"
      :columns="[
        {
          name: 'ts',
          label: 'Timestamp (MYT)',
          field: 'ts',
          align: 'left' as const,
          sortable: true
        },
        {
          name: 'temperatureC',
          label: 'Temp (°C)',
          field: 'temperatureC',
          align: 'right' as const,
          sortable: true
        },
        {
          name: 'humidityPct',
          label: 'Humidity (%)',
          field: 'humidityPct',
          align: 'right' as const,
          sortable: true
        },
        {
          name: 'lightLux',
          label: 'Light (lux)',
          field: 'lightLux',
          align: 'right' as const,
          sortable: true
        },
        {
          name: 'batteryPct',
          label: 'Battery (%)',
          field: 'batteryPct',
          align: 'right' as const,
          sortable: true
        },
        {
          name: 'vibrationG',
          label: 'Vibration (g)',
          field: 'vibrationG',
          align: 'right' as const,
          sortable: true
        },
        {
          name: 'rssiDbm',
          label: 'RSSI (dBm)',
          field: 'rssiDbm',
          align: 'right' as const,
          sortable: true
        }
      ]"
      row-key="ts"
      flat
      dense
      separator="cell"
      style="font-variant-numeric: tabular-nums"
      :class="$style.styledTable"
      :rows-per-page-options="[10, 25, 50, 0]"
      @mouseover="handleMouseOver"
      @mouseleave="handleMouseLeave"
    >

      <template #body-cell-ts="slotProps">
        <q-td auto-width :props="slotProps" style="white-space: nowrap; font-size: 12px">
          {{ fmtTs(slotProps.row.ts) }}
        </q-td>
      </template>
      <template #body-cell-temperatureC="slotProps">
        <q-td auto-width :props="slotProps" class="text-right">
          <span :style="{ color: tempColor(slotProps.row.temperatureC), fontWeight: '700' }">
            {{ props.fmt(slotProps.row.temperatureC, 1) }}
            <q-icon
              v-if="
                tempColor(slotProps.row.temperatureC) === '#d32f2f' ||
                tempColor(slotProps.row.temperatureC) === '#f57c00'
              "
              name="fa-solid fa-triangle-exclamation"
              size="11px"
              class="q-ml-xs"
            />
          </span>
        </q-td>
      </template>
      <template #body-cell-humidityPct="slotProps">
        <q-td auto-width :props="slotProps" class="text-right">
          {{ props.fmt(slotProps.row.humidityPct, 1) }}
        </q-td>
      </template>
      <template #body-cell-lightLux="slotProps">
        <q-td auto-width :props="slotProps" class="text-right">
          {{ props.fmt(slotProps.row.lightLux, 0) }}
        </q-td>
      </template>
      <template #body-cell-batteryPct="slotProps">
        <q-td auto-width :props="slotProps" class="text-right">
          {{ props.fmt(slotProps.row.batteryPct, 0) }}
        </q-td>
      </template>
      <template #body-cell-vibrationG="slotProps">
        <q-td auto-width :props="slotProps" class="text-right">
          {{ slotProps.row.vibrationG != null ? props.fmt(slotProps.row.vibrationG, 3) : "—" }}
        </q-td>
      </template>
      <template #body-cell-rssiDbm="slotProps">
        <q-td auto-width :props="slotProps" class="text-right">
          {{ slotProps.row.rssiDbm != null ? slotProps.row.rssiDbm + " dBm" : "—" }}
        </q-td>
      </template>
      <template #no-data>
        <div class="full-width text-center q-pa-md text-grey-5">No history data</div>
      </template>
    </q-table>
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
</style>
