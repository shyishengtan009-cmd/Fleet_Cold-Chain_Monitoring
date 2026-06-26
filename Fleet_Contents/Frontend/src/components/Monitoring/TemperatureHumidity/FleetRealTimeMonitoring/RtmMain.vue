<script setup lang="ts">
import { onActivated, onMounted, onUnmounted, ref, watch } from "vue";
import {
  Chart as ChartJS,
  LineElement,
  LineController,
  PointElement,
  CategoryScale,
  LinearScale,
  TimeScale,
  Title,
  Tooltip,
  Legend
} from "chart.js";
import "chartjs-adapter-date-fns";
import { useRoute, useRouter } from "vue-router";
import HeaderAppV2 from "@/components/common/HeaderAppV2.vue";
import DeviceDropdown from "../DeviceDropdown.vue";
import RtmDeviceStatus from "./RtmDeviceStatus.vue";
import FleetDevicePortal from "./FleetDevicePortal.vue";
import FleetExportDialog from "./FleetExportDialog.vue";
import { useRtmFleet } from "./useRtmFleet";
import { useRtmHistory } from "./useRtmHistory";
import { useRtmCharts } from "./useRtmCharts";
import { useRtmAlarms } from "./useRtmAlarms";
import { useRtmSettings } from "./useRtmSettings";
import { useAlarmNotifier } from "./useAlarmNotifier";
import { fmt, ageMin, fmtTs } from "./rtmFormat";
import api from "@/helpers/api";

ChartJS.register(
  Title,
  Tooltip,
  Legend,
  LineElement,
  LineController,
  PointElement,
  CategoryScale,
  LinearScale,
  TimeScale
);

// ── Composables ────────────────────────────────────────────────────────────
const {
  fleetItems,
  fleetLoading,
  fleetError,
  fleetUpdated,
  selectedId,
  selectedItem,
  statusCounts,
  fetchFleet,
  selectDevice,
  resetPoll,
  stopPoll
} = useRtmFleet();

const { historyRows, historyLoading, fetchHistory } = useRtmHistory();
const {
  chartTempHum,
  chartTempHumOpts,
  chartLight,
  chartLightOpts,
  chartBattery,
  chartBatteryOpts,
  hasVibration,
  chartVibration,
  chartVibrationOpts,
  hasRssi,
  chartRssi,
  chartRssiOpts
} = useRtmCharts(historyRows);
const { thresholds, trip, fetchSettings } = useRtmSettings();
const { alarms } = useRtmAlarms(selectedItem, thresholds);
const { setDevice: setAlarmDevice, checkNow: checkAlarms } = useAlarmNotifier();

// ── Device portal ───────────────────────────────────────────────────────────
const showPortal = ref(false);
const showExport = ref(false);

async function checkDeviceRegistration() {
  if (import.meta.env.DEV) {
    await api.fleet.seedDevice("HIAS-TEST-001", "HIAS1234").catch(() => {});
  }
  try {
    const result = await api.fleet.getDevices();
    const devices = ((result as any).details ?? result) as unknown[];
    if (!Array.isArray(devices) || devices.length === 0) showPortal.value = true;
  } catch {
    // fall back to fleet check
  }
}

async function onDeviceRegistered(hardwareId: string) {
  showPortal.value = false;
  await fetchFleet();
  if (hardwareId) {
    selectDevice(hardwareId);
    fetchHistory(hardwareId);
    fetchSettings(hardwareId);
    setAlarmDevice(hardwareId, selectedItem.value?.truckName);
  } else if (selectedId.value) {
    fetchHistory(selectedId.value);
    fetchSettings(selectedId.value);
    setAlarmDevice(selectedId.value, selectedItem.value?.truckName);
  }
}

// ── Active tab ─────────────────────────────────────────────────────────────
const router = useRouter();
const route = useRoute();
const activeTab = ref<"all" | "graph" | "history" | "alarm" | "map">("all");

// ── Wire device selection to history fetch ─────────────────────────────────
watch(selectedId, (hw) => {
  if (hw) {
    fetchHistory(hw);
    fetchSettings(hw);
    setAlarmDevice(hw, selectedItem.value?.truckName);
  }
});

// ── Poll tick (shared between interval and visibility-resume) ──────────────
async function pollTick() {
  await fetchFleet();
  if (selectedId.value) {
    fetchHistory(selectedId.value);
    fetchSettings(selectedId.value);
    checkAlarms();
  }
}

// ── Lifecycle ──────────────────────────────────────────────────────────────
onMounted(async () => {
  if (route.path.includes("tt19-fleet/alert")) {
    activeTab.value = "alarm";
  }
  await checkDeviceRegistration();
  await fetchFleet();
  if (selectedId.value) {
    fetchHistory(selectedId.value);
    fetchSettings(selectedId.value);
    setAlarmDevice(selectedId.value, selectedItem.value?.truckName);
  }
  resetPoll(pollTick);
  document.addEventListener("visibilitychange", handleVisibilityChange);
});

onUnmounted(() => {
  stopPoll();
  document.removeEventListener("visibilitychange", handleVisibilityChange);
});

// Re-fetch settings when navigating back from Device Settings (keep-alive or not)
onActivated(() => {
  if (selectedId.value) fetchSettings(selectedId.value);
});

// Pause polling while tab is hidden; resume + immediate refresh on visible
function handleVisibilityChange() {
  if (document.visibilityState === "hidden") {
    stopPoll();
    return;
  }
  void pollTick();
  resetPoll(pollTick);
}

function handleSelectDevice(hw: string) {
  selectDevice(hw);
  fetchHistory(hw);
}
</script>

<template>
  <FleetDevicePortal
    v-if="showPortal"
    @registered="(hw: string) => onDeviceRegistered(hw)"
    @close="showPortal = false"
  />

  <HeaderAppV2
    titlePage="Fleet Real-Time Monitoring"
    :breadcrumbs="['Monitoring', 'Fleet', 'Real-Time Monitoring']"
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

  <div class="app-container q-mx-lg q-mt-lg q-mb-lg">
    <div class="row bg-white">
      <!-- Filter / control section -->
      <div :class="$style.searchContainer">
        <div class="q-pa-md">
          <DeviceDropdown
            :model-value="selectedId ?? ''"
            :devices="fleetItems"
            @update:model-value="handleSelectDevice"
          />
        </div>
        <div class="q-pa-md">
          <q-banner v-if="fleetError" class="bg-red-1 text-negative q-mb-md" rounded dense>
            <template #avatar>
              <q-icon name="fa-solid fa-circle-exclamation" color="negative" />
            </template>
            {{ fleetError }}
          </q-banner>

          <div class="row q-gutter-sm items-center">
            <q-badge color="green-2" text-color="black" class="q-px-sm" style="font-size: 11px">
              <q-icon name="fa-solid fa-circle-check" size="11px" class="q-mr-xs" />
              ACTIVE: {{ statusCounts.ok }}
            </q-badge>
            <q-badge color="orange-2" text-color="black" class="q-px-sm" style="font-size: 11px">
              <q-icon name="fa-solid fa-triangle-exclamation" size="11px" class="q-mr-xs" />
              WARNING: {{ statusCounts.warn }}
            </q-badge>
            <q-badge color="red-2" text-color="black" class="q-px-sm" style="font-size: 11px">
              <q-icon name="fa-solid fa-circle-xmark" size="11px" class="q-mr-xs" />
              OFFLINE: {{ statusCounts.offline }}
            </q-badge>
            <span v-if="fleetUpdated" class="text-caption text-grey-7">
              Updated: {{ fleetUpdated }}
            </span>
            <q-space />
            <q-btn
              outline
              no-caps
              color="white text-black"
              :loading="fleetLoading"
              @click="fetchFleet"
            >
              <q-icon class="q-pr-xs" size="13px" name="fa-solid fa-arrows-rotate" />
              Refresh Now
            </q-btn>
            <q-btn v-if="selectedId" outline no-caps color="primary" @click="showExport = true">
              <q-icon class="q-pr-sm" size="13px" name="fa-solid fa-file-export" />
              Export
            </q-btn>
          </div>
        </div>
      </div>

      <!-- Device status content -->
      <div style="width: 100%">
        <RtmDeviceStatus
          v-if="selectedItem"
          :selected-item="selectedItem"
          :active-tab="activeTab"
          :history-loading="historyLoading"
          :history-rows="historyRows"
          :chart-temp-hum="chartTempHum"
          :chart-temp-hum-opts="chartTempHumOpts"
          :chart-light="chartLight"
          :chart-light-opts="chartLightOpts"
          :chart-battery="chartBattery"
          :chart-battery-opts="chartBatteryOpts"
          :has-vibration="hasVibration"
          :chart-vibration="chartVibration"
          :chart-vibration-opts="chartVibrationOpts"
          :has-rssi="hasRssi"
          :chart-rssi="chartRssi"
          :chart-rssi-opts="chartRssiOpts"
          :alarms="alarms"
          :thresholds="thresholds"
          :trip="trip"
          :fmt="fmt"
          :age-min="ageMin"
          :fmt-ts="fmtTs"
          @update:active-tab="activeTab = $event"
        />

        <div v-else class="text-center text-grey-5 q-pa-xl" style="width: 100%">
          <q-icon name="fa-solid fa-truck" size="48px" class="q-mb-md" />
          <br />
          Select a device from the dropdown above to view details
        </div>
      </div>
    </div>
  </div>

  <FleetExportDialog
    v-if="showExport && selectedId"
    :hardware-id="selectedId"
    @close="showExport = false"
  />
</template>

<style lang="sass" module>
@import '../../../../style/_variables'

.searchContainer
  width: 100%
  background-color: $white
  margin-top: 10px
</style>
