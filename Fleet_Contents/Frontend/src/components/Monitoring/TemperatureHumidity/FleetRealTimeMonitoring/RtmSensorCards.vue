<script setup lang="ts">
import type { AlarmThresholds } from "./useRtmSettings";

import { computed } from "vue";
import { STATUS_ALARM, STATUS_OK, STATUS_WARN } from "@/style/statusColors";

const props = defineProps<{
  selectedItem: any;
  thresholds: AlarmThresholds;
  fmt: (v: number | null | undefined, decimals?: number, suffix?: string) => string;
  ageMin: (s: number | null) => string;
  historyRows?: any[];
}>();

const latestRssi = computed<number | null>(() => {
  if (props.selectedItem.rssiDbm != null) return props.selectedItem.rssiDbm as number;
  if (!props.historyRows?.length) return null;
  for (let i = props.historyRows.length - 1; i >= 0; i--) {
    if (props.historyRows[i].rssiDbm != null) return props.historyRows[i].rssiDbm as number;
  }
  return null;
});

// ── Colour constants matching the pre-established alarm palette ───────────────
const C_OK = STATUS_OK;
const C_WARN = STATUS_WARN;
const C_ALARM = STATUS_ALARM;
const C_NEUTRAL = "#1a1a1a"; // dark   — no threshold configured, can't judge

function tempColor(): string {
  const v = props.selectedItem.temperatureC;
  if (v == null) return C_NEUTRAL;
  const { temp_min_c, temp_max_c } = props.thresholds;
  const hasThreshold = temp_min_c != null || temp_max_c != null;
  if ((temp_max_c != null && v > temp_max_c) || (temp_min_c != null && v < temp_min_c))
    return C_ALARM;
  return hasThreshold ? C_OK : C_NEUTRAL;
}
function humColor(): string {
  const v = props.selectedItem.humidityPct;
  if (v == null) return C_NEUTRAL;
  const { humidity_min_pct, humidity_max_pct } = props.thresholds;
  const hasThreshold = humidity_min_pct != null || humidity_max_pct != null;
  if (
    (humidity_max_pct != null && v > humidity_max_pct) ||
    (humidity_min_pct != null && v < humidity_min_pct)
  )
    return C_WARN;
  return hasThreshold ? C_OK : C_NEUTRAL;
}
function lightColor(): string {
  const v = props.selectedItem.lightLux;
  if (v == null) return C_NEUTRAL;
  const { light_min_lux, light_max_lux } = props.thresholds;
  const hasThreshold = light_min_lux != null || light_max_lux != null;
  if ((light_max_lux != null && v > light_max_lux) || (light_min_lux != null && v < light_min_lux))
    return C_WARN;
  return hasThreshold ? C_OK : C_NEUTRAL;
}
function batColor(): string {
  const v = props.selectedItem.batteryPct;
  if (v == null) return C_NEUTRAL;
  const { battery_min_pct } = props.thresholds;
  if (battery_min_pct != null && v < battery_min_pct) return C_ALARM;
  return battery_min_pct != null ? C_OK : C_NEUTRAL;
}
function ageColor(): string {
  const s = props.selectedItem.ageSeconds;
  if (s == null) return C_NEUTRAL;
  if (s < 15 * 60) return C_OK;
  if (s < 30 * 60) return C_WARN;
  return C_ALARM;
}
function rssiColor(): string {
  const v = latestRssi.value;
  if (v == null) return C_NEUTRAL;
  if (v > -70) return C_OK;
  if (v > -85) return C_WARN;
  return C_ALARM;
}
</script>

<template>
  <div class="row q-pa-md gap-sm" :class="$style.sensorGrid">
    <div :class="$style.sensorCard">
      <q-icon name="fa-solid fa-temperature-half" size="22px" color="blue-6" />
      <div class="text-caption text-grey-6 q-mt-xs">Temperature</div>
      <div :class="$style.sensorVal" :style="{ color: tempColor() }">
        {{ props.fmt(props.selectedItem.temperatureC, 1, "°C") }}
      </div>
      <q-icon
        v-if="tempColor() === C_ALARM"
        name="fa-solid fa-triangle-exclamation"
        size="12px"
        color="negative"
        class="q-mt-xs"
      />
    </div>

    <div :class="$style.sensorCard">
      <q-icon name="fa-solid fa-droplet" size="22px" color="cyan-6" />
      <div class="text-caption text-grey-6 q-mt-xs">Humidity</div>
      <div :class="$style.sensorVal" :style="{ color: humColor() }">
        {{ props.fmt(props.selectedItem.humidityPct, 1, "%") }}
      </div>
      <q-icon
        v-if="humColor() === C_WARN"
        name="fa-solid fa-triangle-exclamation"
        size="12px"
        color="warning"
        class="q-mt-xs"
      />
    </div>

    <div :class="$style.sensorCard">
      <q-icon name="fa-solid fa-sun" size="22px" color="yellow-8" />
      <div class="text-caption text-grey-6 q-mt-xs">Light</div>
      <div :class="$style.sensorVal" :style="{ color: lightColor() }">
        {{ props.fmt(props.selectedItem.lightLux, 0, " lux") }}
      </div>
      <q-icon
        v-if="lightColor() === C_WARN"
        name="fa-solid fa-triangle-exclamation"
        size="12px"
        color="warning"
        class="q-mt-xs"
      />
    </div>

    <div :class="$style.sensorCard">
      <q-icon name="fa-solid fa-battery-three-quarters" size="22px" color="green-6" />
      <div class="text-caption text-grey-6 q-mt-xs">Battery</div>
      <div :class="$style.sensorVal" :style="{ color: batColor() }">
        {{ props.fmt(props.selectedItem.batteryPct, 0, "%") }}
      </div>
      <q-icon
        v-if="batColor() === C_ALARM"
        name="fa-solid fa-triangle-exclamation"
        size="12px"
        color="negative"
        class="q-mt-xs"
      />
    </div>

    <div :class="$style.sensorCard">
      <q-icon name="fa-solid fa-wave-square" size="22px" color="orange-6" />
      <div class="text-caption text-grey-6 q-mt-xs">Age</div>
      <div :class="$style.sensorVal" :style="{ color: ageColor() }">
        {{ props.ageMin(props.selectedItem.ageSeconds) }} min
      </div>
      <q-icon
        v-if="ageColor() === C_WARN || ageColor() === C_ALARM"
        name="fa-solid fa-triangle-exclamation"
        size="12px"
        :color="ageColor() === C_ALARM ? 'negative' : 'warning'"
        class="q-mt-xs"
      />
    </div>

    <div :class="$style.sensorCard">
      <q-icon name="fa-solid fa-signal" size="22px" color="teal-6" />
      <div class="text-caption text-grey-6 q-mt-xs">RSSI</div>
      <div :class="$style.sensorVal" :style="{ color: rssiColor() }">
        {{ latestRssi != null ? latestRssi + " dBm" : "—" }}
      </div>
      <q-icon
        v-if="rssiColor() === C_WARN || rssiColor() === C_ALARM"
        name="fa-solid fa-triangle-exclamation"
        size="12px"
        :color="rssiColor() === C_ALARM ? 'negative' : 'warning'"
        class="q-mt-xs"
      />
    </div>

    <div v-if="props.selectedItem.plate" :class="$style.sensorCard">
      <q-icon name="fa-solid fa-id-card" size="22px" color="purple-6" />
      <div class="text-caption text-grey-6 q-mt-xs">Plate</div>
      <div :class="$style.sensorVal">
        {{ props.selectedItem.plate }}
      </div>
    </div>
  </div>
</template>

<style lang="sass" module>
@import '../../../../style/_variables'

.sensorGrid
  display: grid
  grid-template-columns: repeat(6, 1fr)
  gap: 16px
  width: 100%

.sensorCard
  display: flex
  flex-direction: column
  align-items: center
  justify-content: center
  width: 100%
  padding: 22px 16px
  border: 1px solid $secondary-grey-2
  border-radius: 12px
  background: $primary-grey-1
  text-align: center

.sensorVal
  font-size: 30px
  font-weight: 700
  margin-top: 8px
  font-variant-numeric: tabular-nums

/* Layout positioning */
.sensorGrid > :nth-child(1)
  grid-column: 1 / 3

.sensorGrid > :nth-child(2)
  grid-column: 3 / 5

.sensorGrid > :nth-child(3)
  grid-column: 5 / 7

.sensorGrid > :nth-child(4)
  grid-column: 1 / 3

.sensorGrid > :nth-child(5)
  grid-column: 3 / 5

.sensorGrid > :nth-child(6)
  grid-column: 5 / 7
</style>
