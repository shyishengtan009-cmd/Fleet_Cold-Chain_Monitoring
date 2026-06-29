<script setup lang="ts">
import { ref } from "vue";
import { connectivityBadge } from "../Fleet/useFleetConnectivityStatus";
import { STATUS_ALARM, STATUS_INFO, STATUS_WARN } from "@/style/statusColors";

const selectRef = ref<{ hidePopup: () => void } | null>(null);
function onScroll() { selectRef.value?.hidePopup(); }
function onShow() { window.addEventListener("scroll", onScroll, true); }
function onHide() { window.removeEventListener("scroll", onScroll, true); }

const props = defineProps<{
  selectedId: string | null;
  fleetItems: any[];
  fleetLoading: boolean;
  fleetError: string | null;
  fleetUpdated: string | null;
  columns: any[];
  refreshSec: number;
  warnMin: number;
  offlineMin: number;
  statusCounts: { ok: number; warn: number; offline: number };
  thresholds: any;
  selectDevice: (hw: string) => void;
  applySettings: () => void;
  fetchFleet: () => void;
  fmt: (v: number | null | undefined, decimals?: number, suffix?: string) => string;
  ageMin: (s: number | null) => string;
}>();

function tempCellColor(v: number | null): string {
  if (v == null) return "";
  const t = props.thresholds ?? {};
  const min = t.temp_min_c ?? null;
  const max = t.temp_max_c ?? null;
  const range = (max ?? 40) - (min ?? 0);
  const warnAt = (max ?? 40) - range * 0.2;
  if (max != null && v >= max) return STATUS_ALARM;
  if (min != null && v <= min) return STATUS_INFO;
  if (max != null && v >= warnAt) return STATUS_WARN;
  return "#1976d2";
}
</script>

<template>
  <q-card flat class="q-mb-md">
    <div class="row items-center gap-sm q-pa-md bd-b flex-wrap">
      <!-- Device selector -->
      <q-select
        ref="selectRef"
        :model-value="props.selectedId"
        @update:model-value="(v) => props.selectDevice(v)"
        :options="
          props.fleetItems.map((i) => ({
            label: i.truckName ? `${i.truckName} — ${i.hardwareId}` : i.hardwareId,
            value: i.hardwareId
          }))
        "
        option-value="value"
        option-label="label"
        emit-value
        map-options
        dense
        outlined
        label="Device"
        class="inputSearchTable"
        style="min-width: 240px"
        @popup-show="onShow"
        @popup-hide="onHide"
      />

      <!-- Refresh -->
      <q-input
        v-model.number="props.refreshSec"
        label="Refresh (sec)"
        type="number"
        dense
        outlined
        class="inputSearchTable"
        style="width: 110px"
      />
      <q-input
        v-model.number="props.warnMin"
        label="WARN ≥ (min)"
        type="number"
        dense
        outlined
        class="inputSearchTable"
        style="width: 110px"
      />
      <q-input
        v-model.number="props.offlineMin"
        label="OFFLINE ≥ (min)"
        type="number"
        dense
        outlined
        class="inputSearchTable"
        style="width: 130px"
      />
      <q-btn color="primary" no-caps dense @click="props.applySettings">
        <q-icon class="q-pr-xs" size="13px" name="fa-solid fa-check" />
        Apply
      </q-btn>
      <q-btn
        color="secondary"
        no-caps
        dense
        :loading="props.fleetLoading"
        @click="props.fetchFleet"
      >
        <q-icon class="q-pr-xs" size="13px" name="fa-solid fa-arrows-rotate" />
        Refresh Now
      </q-btn>

      <q-space />

      <!-- Status chips -->
      <q-chip dense square :color="connectivityBadge('OK').color" :text-color="connectivityBadge('OK').textColor">
        <q-icon :name="connectivityBadge('OK').icon" class="q-mr-xs" size="12px" />
        {{ connectivityBadge("OK").label }}: {{ props.statusCounts.ok }}
      </q-chip>
      <q-chip
        dense
        square
        :color="connectivityBadge('WARN').color"
        :text-color="connectivityBadge('WARN').textColor"
      >
        <q-icon :name="connectivityBadge('WARN').icon" class="q-mr-xs" size="12px" />
        {{ connectivityBadge("WARN").label }}: {{ props.statusCounts.warn }}
      </q-chip>
      <q-chip
        dense
        square
        :color="connectivityBadge('OFFLINE').color"
        :text-color="connectivityBadge('OFFLINE').textColor"
      >
        <q-icon :name="connectivityBadge('OFFLINE').icon" class="q-mr-xs" size="12px" />
        {{ connectivityBadge("OFFLINE").label }}: {{ props.statusCounts.offline }}
      </q-chip>

      <span v-if="props.fleetUpdated" class="text-caption text-grey-5">
        Updated: {{ props.fleetUpdated }}
      </span>
    </div>

    <!-- Error -->
    <q-banner v-if="props.fleetError" class="q-ma-md bg-red-1 text-red-9 radius-5" dense rounded>
      <template #avatar>
        <q-icon name="fa-solid fa-circle-exclamation" color="negative" />
      </template>
      {{ props.fleetError }}
    </q-banner>

    <!-- Fleet table -->
    <q-table
      :rows="props.fleetItems ?? []"
      :columns="props.columns"
      row-key="hardwareId"
      :loading="props.fleetLoading"
      flat
      dense
      separator="cell"
      style="font-variant-numeric: tabular-nums"
      :rows-per-page-options="[10, 25, 50, 0]"
      rows-per-page-label="Rows per page"
      :selected-rows-label="() => ''"
      @row-click="(_, row) => props.selectDevice(row.hardwareId)"
      :class="[$style.styledTable, $style.clickableRows]"
    >
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
      <template #body-cell-truckName="slotProps">
        <q-td :props="slotProps">
          <span v-if="slotProps.row.truckName">{{ slotProps.row.truckName }}</span>
          <span v-else class="text-grey-5 text-italic">Unassigned</span>
        </q-td>
      </template>
      <template #body-cell-plate="slotProps">
        <q-td :props="slotProps">
          <span v-if="slotProps.row.plate">{{ slotProps.row.plate }}</span>
          <span v-else class="text-grey-5">—</span>
        </q-td>
      </template>
      <template #body-cell-temperatureC="slotProps">
        <q-td :props="slotProps" class="text-right">
          <span :style="{ color: tempCellColor(slotProps.row.temperatureC), fontWeight: '700' }">
            {{ props.fmt(slotProps.row.temperatureC, 1, "°C") }}
            <q-icon
              v-if="
                tempCellColor(slotProps.row.temperatureC) === '#d32f2f' ||
                tempCellColor(slotProps.row.temperatureC) === '#f57c00'
              "
              name="fa-solid fa-triangle-exclamation"
              size="11px"
              class="q-ml-xs"
            />
          </span>
        </q-td>
      </template>
      <template #body-cell-humidityPct="slotProps">
        <q-td :props="slotProps" class="text-right">
          {{ props.fmt(slotProps.row.humidityPct, 1, "%") }}
        </q-td>
      </template>
      <template #body-cell-lightLux="slotProps">
        <q-td :props="slotProps" class="text-right">
          {{ props.fmt(slotProps.row.lightLux, 0) }}
        </q-td>
      </template>
      <template #body-cell-batteryPct="slotProps">
        <q-td :props="slotProps" class="text-right">
          {{ props.fmt(slotProps.row.batteryPct, 0, "%") }}
        </q-td>
      </template>
      <template #body-cell-ageSeconds="slotProps">
        <q-td :props="slotProps" class="text-right">
          {{ props.ageMin(slotProps.row.ageSeconds) }}
        </q-td>
      </template>
      <template #no-data>
        <div class="full-width text-center q-pa-lg text-grey-5" style="font-size: 16px">
          <q-icon name="fa-solid fa-satellite-dish" size="40px" class="q-mb-sm" />
          <br />
          No fleet data available
        </div>
      </template>
    </q-table>
  </q-card>
</template>

<style lang="sass" module>
@import '../../../../style/_variables'

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

.clickableRows
  :global(tbody tr)
    cursor: pointer
  :global(tbody tr:hover td)
    background: $primary-grey-1 !important
</style>
