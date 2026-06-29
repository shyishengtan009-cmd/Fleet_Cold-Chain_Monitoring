<script setup lang="ts">
import { ref, watch } from "vue";
import api from "@/helpers/api";
import { STATUS_ALARM, STATUS_INFO, STATUS_OK, STATUS_WARN } from "@/style/statusColors";

const props = defineProps<{ hardwareId: string }>();

type BatteryForecast = {
  currentPct: number;
  slopePerHour: number;
  hoursUntilThreshold: number | null;
  thresholdPct: number;
  dataPoints: number;
  status: "Charging" | "Stable" | "Discharging" | "Critical";
};

const forecast = ref<BatteryForecast | null>(null);
const loading = ref(false);

async function fetchForecast() {
  loading.value = true;
  try {
    const res = (await api.fleet.getBatteryForecast(props.hardwareId)) as any;
    forecast.value = res?.details?.forecast ?? null;
  } catch {
    forecast.value = null;
  } finally {
    loading.value = false;
  }
}

watch(
  () => props.hardwareId,
  (hw) => {
    if (hw) fetchForecast();
  },
  { immediate: true }
);

function fmtHours(h: number | null): string {
  if (h == null) return "—";
  if (h >= 48) return `${Math.round(h / 24)} d`;
  if (h >= 1) return `${h.toFixed(1)} h`;
  return `${Math.round(h * 60)} min`;
}

const STATUS_COLOR: Record<string, string> = {
  Charging: STATUS_OK,
  Stable: STATUS_INFO,
  Discharging: STATUS_WARN,
  Critical: STATUS_ALARM
};
</script>

<template>
  <div v-if="loading || forecast" class="q-px-md q-pb-md">
    <div class="row items-center gap-sm q-mb-sm">
      <q-icon name="fa-solid fa-chart-line" size="13px" color="grey-6" />
      <span class="text-caption text-grey-6" style="font-size: 12px">
        Battery Forecast (48 h window)
      </span>
      <q-spinner v-if="loading" size="12px" color="grey-6" class="q-ml-xs" />
    </div>

    <div v-if="forecast" class="row gap-sm" style="flex-wrap: wrap">
      <div :class="$style.fcCard">
        <div class="text-caption text-grey-6">Status</div>
        <div :class="$style.fcVal" :style="{ color: STATUS_COLOR[forecast.status] ?? '#555' }">
          {{ forecast.status }}
        </div>
      </div>

      <div :class="$style.fcCard">
        <div class="text-caption text-grey-6">Slope</div>
        <div
          :class="$style.fcVal"
          :style="{ color: forecast.slopePerHour < -0.1 ? STATUS_ALARM : STATUS_OK }"
        >
          {{ forecast.slopePerHour >= 0 ? "+" : "" }}{{ forecast.slopePerHour.toFixed(2) }} %/h
        </div>
      </div>

      <div :class="$style.fcCard">
        <div class="text-caption text-grey-6">Reaches {{ forecast.thresholdPct }}% in</div>
        <div :class="$style.fcVal">{{ fmtHours(forecast.hoursUntilThreshold) }}</div>
      </div>

      <div :class="$style.fcCard">
        <div class="text-caption text-grey-6">Data Points</div>
        <div :class="$style.fcVal">{{ forecast.dataPoints }}</div>
      </div>
    </div>

    <div v-else-if="!loading" class="text-caption text-grey-5">
      Not enough data for forecast (need ≥ 3 readings in last 48 h)
    </div>
  </div>
</template>

<style lang="sass" module>
@import '../../../../style/_variables'

.fcCard
  display: flex
  flex-direction: column
  padding: 8px 14px
  border: 1px solid $secondary-grey-2
  border-radius: 8px
  background: $primary-grey-1
  min-width: 120px

.fcVal
  font-size: $font-size-md
  font-weight: 700
  margin-top: 2px
  font-variant-numeric: tabular-nums
</style>
