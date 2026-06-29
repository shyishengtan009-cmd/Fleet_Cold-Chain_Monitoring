<script setup lang="ts">
import { computed } from "vue";
import RtmAll from "./RtmAll.vue";
import RtmGraph from "./RtmGraph.vue";
import RtmHistory from "./RtmHistory.vue";
import RtmAlarm from "./RtmAlarm.vue";
import RtmSensorCards from "./RtmSensorCards.vue";
import RtmBatteryForecast from "./RtmBatteryForecast.vue";
import TripRouteIndicator from "./TripRouteIndicator.vue";
import type { AlarmThresholds } from "./useRtmSettings";
import RtmMap from "./RtmMap.vue";
const props = defineProps<{
  selectedItem: any | null;
  activeTab: "all" | "graph" | "history" | "alarm" | "map";
  historyLoading: boolean;
  historyRows: any[];
  chartTempHum: any;
  chartTempHumOpts: any;
  chartLight: any;
  chartLightOpts: any;
  chartBattery: any;
  chartBatteryOpts: any;
  hasVibration: boolean;
  chartVibration: any;
  chartVibrationOpts: any;
  hasRssi: boolean;
  chartRssi: any;
  chartRssiOpts: any;
  alarms: { field: string; value: string; level: string }[];
  thresholds: AlarmThresholds;
  trip: any;
  fmt: (v: number | null | undefined, decimals?: number, suffix?: string) => string;
  ageMin: (s: number | null) => string;
  fmtTs: (iso: string | null) => string;
}>();

const emit = defineEmits<{
  (e: "update:activeTab", value: "all" | "graph" | "history" | "alarm" | "map"): void;
}>();

const activeTabModel = computed({
  get: () => props.activeTab,
  set: (val: "all" | "graph" | "history" | "alarm" | "map") => emit("update:activeTab", val)
});

const statusColor = computed(() => {
  if (props.selectedItem?.status === "OK") return "positive";
  if (props.selectedItem?.status === "WARN") return "warning";
  return "negative";
});
</script>

<template>
  <q-card v-if="props.selectedItem" flat>
    <!-- Device header -->
    <q-card-section class="headerColor text-weight-bold">
      <div class="row items-center gap-sm">
        <q-icon name="fa-solid fa-truck" size="18px" color="primary" />
        <span class="device-title">
          {{ props.selectedItem.truckName ?? "Unassigned" }}
          <span class="text-grey-6 text-weight-regular q-ml-xs device-subtitle">
            {{ props.selectedItem.hardwareId }}
          </span>
        </span>
        <q-chip dense square :color="statusColor" text-color="white" class="status-chip">
          {{ props.selectedItem.status }}
        </q-chip>
        <q-space />
        <span class="text-caption text-grey-6 text-weight-regular">
          Last seen: {{ props.fmtTs(props.selectedItem.ts) }}
        </span>
      </div>
    </q-card-section>
    <q-separator />

    <!-- Sensor cards -->
    <RtmSensorCards
      :selected-item="props.selectedItem"
      :thresholds="props.thresholds"
      :fmt="props.fmt"
      :age-min="props.ageMin"
      :history-rows="props.historyRows"
    />
    <q-separator />
    <RtmBatteryForecast :hardware-id="props.selectedItem.hardwareId" />
    <TripRouteIndicator :trip="props.trip" />
    <q-separator />

    <!-- Tabs -->
    <q-tabs
      v-model="activeTabModel"
      dense
      no-caps
      align="left"
      class="q-px-md rtm-tabs"
      active-color="primary"
      indicator-color="primary"
    >
      <q-tab name="all" label="Shipment Info" />
      <q-tab name="graph" label="Graph" />
      <q-tab name="history" label="History" />
      <q-tab name="alarm" label="Alarm" />
      <q-tab name="map" label="Map" />
    </q-tabs>

    <q-tab-panels v-model="activeTabModel" animated>
      <q-tab-panel name="all" class="q-pa-none">
        <RtmAll :trip="props.trip" />
      </q-tab-panel>

      <q-tab-panel name="graph" class="q-pa-none">
        <RtmGraph
          :history-loading="props.historyLoading"
          :history-rows="props.historyRows"
          :chart-temp-hum="props.chartTempHum"
          :chart-temp-hum-opts="props.chartTempHumOpts"
          :chart-light="props.chartLight"
          :chart-light-opts="props.chartLightOpts"
          :chart-battery="props.chartBattery"
          :chart-battery-opts="props.chartBatteryOpts"
          :has-vibration="props.hasVibration"
          :chart-vibration="props.chartVibration"
          :chart-vibration-opts="props.chartVibrationOpts"
          :has-rssi="props.hasRssi"
          :chart-rssi="props.chartRssi"
          :chart-rssi-opts="props.chartRssiOpts"
        />
      </q-tab-panel>

      <q-tab-panel name="history" class="q-pa-none">
        <RtmHistory
          :history-loading="props.historyLoading"
          :history-rows="props.historyRows"
          :thresholds="props.thresholds"
          :fmt="props.fmt"
          :fmt-ts="props.fmtTs"
        />
      </q-tab-panel>

      <q-tab-panel name="alarm" class="q-pa-none">
        <RtmAlarm
          :alarms="props.alarms"
          :selected-item="props.selectedItem"
          :thresholds="props.thresholds"
          :history-rows="props.historyRows"
          :fmt-ts="props.fmtTs"
        />
      </q-tab-panel>

      <q-tab-panel name="map" class="q-pa-none">
        <RtmMap
          :hardware-id="props.selectedItem.hardwareId"
          :history-rows="props.historyRows"
          :truck-name="props.selectedItem.truckName"
        />
      </q-tab-panel>
    </q-tab-panels>
  </q-card>
</template>

<style lang="sass" scoped>
@import '../../../../style/_variables'

.device-title
  font-size: $font-size-md

.device-subtitle
  font-size: $font-size-sm

.status-chip
  font-size: $font-size-xs

.rtm-tabs
  border-bottom: 1px solid $secondary-grey-2
</style>
