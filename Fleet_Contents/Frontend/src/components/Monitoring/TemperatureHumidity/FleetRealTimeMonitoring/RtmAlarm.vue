<script setup lang="ts">
import type { AlarmThresholds } from "./useRtmSettings";

const props = defineProps<{
  alarms: { field: string; value: string; level: string }[];
  selectedItem: any;
  thresholds: AlarmThresholds;
  historyRows: any[];
  fmtTs: (iso: string | null) => string;
}>();

function tempZoneColor(v: number | null): string {
  if (v == null) return "grey-4";
  const { temp_min_c, temp_max_c } = props.thresholds;
  const range = (temp_max_c ?? 40) - (temp_min_c ?? 0);
  const warnAt = (temp_max_c ?? 40) - range * 0.2;
  if (temp_max_c != null && v >= temp_max_c) return "negative";
  if (temp_min_c != null && v <= temp_min_c) return "blue-9";
  if (temp_max_c != null && v >= warnAt) return "warning";
  return "primary";
}

function tempZoneLabel(v: number | null): string {
  if (v == null) return "No Data";
  const { temp_min_c, temp_max_c } = props.thresholds;
  const range = (temp_max_c ?? 40) - (temp_min_c ?? 0);
  const warnAt = (temp_max_c ?? 40) - range * 0.2;
  if (temp_max_c != null && v >= temp_max_c) return "ALARM — Too High";
  if (temp_min_c != null && v <= temp_min_c) return "ALARM — Too Low";
  if (temp_max_c != null && v >= warnAt) return "WARNING — Approaching Max";
  return "OPTIMAL";
}

function tempZoneTextClass(v: number | null): string {
  if (v == null) return "text-grey-5";
  const { temp_min_c, temp_max_c } = props.thresholds;
  const range = (temp_max_c ?? 40) - (temp_min_c ?? 0);
  const warnAt = (temp_max_c ?? 40) - range * 0.2;
  if (temp_max_c != null && v >= temp_max_c) return "text-negative";
  if (temp_min_c != null && v <= temp_min_c) return "text-blue-9";
  if (temp_max_c != null && v >= warnAt) return "text-warning";
  return "text-primary";
}

function tempRowAlert(v: number | null): boolean {
  if (v == null) return false;
  const { temp_min_c, temp_max_c } = props.thresholds;
  const range = (temp_max_c ?? 40) - (temp_min_c ?? 0);
  const warnAt = (temp_max_c ?? 40) - range * 0.2;
  return (temp_max_c != null && v >= warnAt) || (temp_min_c != null && v <= temp_min_c);
}

const temp = () => props.selectedItem?.temperatureC ?? null;
const fmt1 = (v: number | null, suffix = "") => (v == null ? "—" : `${v.toFixed(1)}${suffix}`);
const last5 = () => [...props.historyRows].reverse().slice(0, 5);
</script>

<template>
  <div class="column">
    <!-- Temperature Big Card -->
    <q-card flat>
      <q-card-section class="headerColor text-weight-bold">
        <div class="row items-center gap-sm">
          <q-icon name="fa-solid fa-temperature-half" size="16px" />
          Temperature
          <q-space />
          <q-badge :color="tempZoneColor(temp())" text-color="white" style="font-weight: 700">
            {{ tempZoneLabel(temp()) }}
          </q-badge>
        </div>
      </q-card-section>
      <q-separator />
      <q-card-section class="q-pa-none">
        <div class="row items-stretch bg-white">
          <!-- Current value -->
          <div
            class="col column items-center justify-center q-py-lg"
            :class="$style.colDivider"
          >
            <div class="text-caption text-grey-6 q-mb-xs">Current</div>
            <div
              :class="tempZoneTextClass(temp())"
              style="font-size: 56px; font-weight: 800; font-variant-numeric: tabular-nums; line-height: 1"
            >
              {{ fmt1(temp()) }}
            </div>
            <div class="text-grey-5" style="font-size: 18px; margin-top: 4px">°C</div>
          </div>
          <!-- Thresholds + legend -->
          <div class="col column items-center justify-center q-py-lg" style="gap: 12px">
            <div class="row items-center" style="gap: 32px">
              <div class="column items-center">
                <div class="text-caption text-grey-5">Min Threshold</div>
                <div class="text-blue-9 text-weight-bold" style="font-size: 28px">
                  {{ thresholds.temp_min_c != null ? thresholds.temp_min_c + "°C" : "—" }}
                </div>
              </div>
              <div :class="$style.vertDivider" />
              <div class="column items-center">
                <div class="text-caption text-grey-5">Max Threshold</div>
                <div class="text-negative text-weight-bold" style="font-size: 28px">
                  {{ thresholds.temp_max_c != null ? thresholds.temp_max_c + "°C" : "—" }}
                </div>
              </div>
            </div>
            <div
              class="row items-center q-mt-xs"
              style="gap: 8px; flex-wrap: wrap; justify-content: center"
            >
              <span
                v-for="z in [
                  { bgClass: 'bg-blue-9', label: 'At/Below Min' },
                  { bgClass: 'bg-primary', label: 'Optimal' },
                  { bgClass: 'bg-warning', label: 'Approaching Max' },
                  { bgClass: 'bg-negative', label: 'At/Above Max' }
                ]"
                :key="z.label"
                class="text-grey-7"
                style="display: flex; align-items: center; gap: 4px; font-size: 11px"
              >
                <span
                  :class="z.bgClass"
                  style="width: 10px; height: 10px; border-radius: 50%; display: inline-block"
                />
                {{ z.label }}
              </span>
            </div>
          </div>
        </div>
      </q-card-section>
    </q-card>

    <!-- Active Alarms -->
    <q-card flat>
      <q-card-section class="headerColor text-weight-bold">Active Alarms</q-card-section>
      <q-separator />
      <q-card-section v-if="alarms.length === 0" class="text-center text-grey-5">
        <q-icon name="fa-solid fa-circle-check" size="24px" color="positive" class="q-mb-xs" />
        <br />
        No active alarms
      </q-card-section>
      <q-list v-else separator>
        <q-item v-for="(a, i) in alarms" :key="i">
          <q-item-section avatar>
            <q-icon
              :name="
                a.level === 'ALARM'
                  ? 'fa-solid fa-circle-xmark'
                  : 'fa-solid fa-triangle-exclamation'
              "
              :color="a.level === 'ALARM' ? 'negative' : 'warning'"
              size="18px"
            />
          </q-item-section>
          <q-item-section>
            <q-item-label>{{ a.field }}</q-item-label>
            <q-item-label caption>{{ a.value }}</q-item-label>
          </q-item-section>
          <q-item-section side>
            <q-badge :color="a.level === 'ALARM' ? 'negative' : 'warning'" :label="a.level" />
          </q-item-section>
        </q-item>
      </q-list>
    </q-card>

    <!-- Last 5 Temperature Readings -->
    <q-card flat>
      <q-card-section class="headerColor text-weight-bold">
        Last 5 Temperature Readings
      </q-card-section>
      <q-separator />
      <div v-if="!last5().length" class="text-center text-grey-5 q-pa-md" style="font-size: 13px">
        No history data
      </div>
      <q-table
        v-else
        :rows="last5()"
        :columns="[
          { name: 'ts', label: 'Date & Time', field: 'ts', align: 'left' as const },
          {
            name: 'temperatureC',
            label: 'Temperature',
            field: 'temperatureC',
            align: 'right' as const
          }
        ]"
        row-key="ts"
        flat
        dense
        hide-bottom
        separator="cell"
        style="font-variant-numeric: tabular-nums"
        :class="$style.styledTable"
      >
        <template #body-cell-ts="slotProps">
          <q-td auto-width :props="slotProps" style="white-space: nowrap; font-size: 12px">
            {{ fmtTs(slotProps.row.ts) }}
          </q-td>
        </template>
        <template #body-cell-temperatureC="slotProps">
          <q-td auto-width :props="slotProps" class="text-right">
            <span :class="[tempZoneTextClass(slotProps.row.temperatureC), 'text-weight-bold']">
              {{
                slotProps.row.temperatureC != null
                  ? slotProps.row.temperatureC.toFixed(1) + "°C"
                  : "—"
              }}
              <q-icon
                v-if="tempRowAlert(slotProps.row.temperatureC)"
                name="fa-solid fa-triangle-exclamation"
                size="11px"
                class="q-ml-xs"
              />
            </span>
          </q-td>
        </template>
      </q-table>
    </q-card>
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

.colDivider
  border-right: 1px solid $primary-grey-1

.vertDivider
  width: 1px
  height: 40px
  background: $secondary-grey-2
</style>
