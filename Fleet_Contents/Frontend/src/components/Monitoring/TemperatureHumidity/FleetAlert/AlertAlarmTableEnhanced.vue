<script setup lang="ts">
import { computed, ref } from "vue";
import { useRowHighlight } from "@/helpers/rowHighlight";
import {
  connectivityBadge,
  type ConnectivityStatus
} from "../Fleet/useFleetConnectivityStatus";

type SimpleAlarm = {
  id: string;
  hardwareId: string;
  label: string;
  truckName?: string;
  field: string;
  value: number;
  thresholdMin: number | null;
  thresholdMax: number | null;
  level: "ALARM" | "WARN" | "OK";
  ts: string;
};

type SensorReading = {
  id: string;
  hardwareId: string;
  label: string;
  truckName: string;
  ts: string;
  temperature: number | null;
  humidity: number | null;
  light: number | null;
  battery: number | null;
  temperatureStatus: string;
  humidityStatus: string;
  lightStatus: string;
  batteryStatus: string;
};

const props = defineProps<{
  alarms: SimpleAlarm[];
  loading: boolean;
  truckName?: string;
  deviceStatus?: ConnectivityStatus | null;
  thresholds: {
    temp_min_c: number | null;
    temp_max_c: number | null;
    humidity_min_pct: number | null;
    humidity_max_pct: number | null;
    light_min_lux: number | null;
    light_max_lux: number | null;
    vibration_g: number | null;
    battery_min_pct: number | null;
  };
}>();

const { handleMouseOver, handleMouseLeave } = useRowHighlight();
const mouseOverId = ref<string>("");

const pagination = ref({
  rowsPerPage: 20,
  sortBy: "ts" as const,
  descending: true,
  page: 1
});

const columns = [
  { name: "ts", label: "Time", field: "ts", align: "left" as const, sortable: true },
  {
    name: "truckName",
    label: "Truck Name",
    field: "truckName",
    align: "left" as const,
    sortable: true
  },
  { name: "device", label: "Device", field: "label", align: "left" as const, sortable: true },
  {
    name: "temperature",
    label: "Temperature",
    field: "temperature",
    align: "center" as const,
    sortable: true
  },
  {
    name: "humidity",
    label: "Humidity",
    field: "humidity",
    align: "center" as const,
    sortable: true
  },
  { name: "light", label: "Light", field: "light", align: "center" as const, sortable: true },
  { name: "battery", label: "Battery", field: "battery", align: "center" as const, sortable: true },
  {
    // Device connectivity status (ACTIVE/WARNING/OFFLINE) — same for every row since
    // it reflects the device's current state, not this specific historical reading.
    // Not sortable: sorting by an identical value across all rows does nothing useful.
    name: "overall",
    label: "Activity",
    field: () => "",
    align: "center" as const,
    sortable: false
  }
];

const sensorReadings = computed((): SensorReading[] => {
  const grouped = new Map<string, SensorReading>();
  const resolvedTruckName = props.truckName || "";

  props.alarms.forEach((alarm) => {
    const key = `${alarm.hardwareId}-${alarm.ts}`;

    if (!grouped.has(key)) {
      grouped.set(key, {
        id: key,
        hardwareId: alarm.hardwareId,
        label: alarm.hardwareId,
        truckName: alarm.truckName || resolvedTruckName,
        ts: alarm.ts,
        temperature: null,
        humidity: null,
        light: null,
        battery: null,
        temperatureStatus: "OPTIMAL",
        humidityStatus: "OPTIMAL",
        lightStatus: "OPTIMAL",
        batteryStatus: "OPTIMAL"
      });
    }

    const reading = grouped.get(key)!;

    switch (alarm.field) {
      case "temperature":
        reading.temperature = alarm.value;
        reading.temperatureStatus = getStatusForField(alarm);
        break;
      case "humidity":
        reading.humidity = alarm.value;
        reading.humidityStatus = getStatusForField(alarm);
        break;
      case "light":
        reading.light = alarm.value;
        reading.lightStatus = getStatusForField(alarm);
        break;
      case "battery":
        reading.battery = alarm.value;
        reading.batteryStatus = getStatusForField(alarm);
        break;
    }

  });

  return Array.from(grouped.values()).sort(
    (a, b) => new Date(b.ts).getTime() - new Date(a.ts).getTime()
  );
});

function getStatusForField(alarm: SimpleAlarm): string {
  const t = props.thresholds;

  if (alarm.field === "temperature") {
    const v = alarm.value;
    const min = t.temp_min_c;
    const max = t.temp_max_c;
    if (max != null && v >= max) return "ALARM ▲";
    if (min != null && v <= min) return "ALARM ▼";
    if (max != null && min != null) {
      const range = max - min;
      if (v >= max - range * 0.2) return "WARN ▲";
    }
    return "OK";
  }
  if (alarm.field === "humidity") {
    const v = alarm.value;
    if (t.humidity_max_pct != null && v >= t.humidity_max_pct) return "WARN ▲";
    if (t.humidity_min_pct != null && v <= t.humidity_min_pct) return "WARN ▼";
    return "OK";
  }
  if (alarm.field === "light") {
    const v = alarm.value;
    if (t.light_max_lux != null && v >= t.light_max_lux) return "WARN ▲";
    if (t.light_min_lux != null && v <= t.light_min_lux) return "WARN ▼";
    return "OK";
  }
  if (alarm.field === "battery") {
    if (t.battery_min_pct != null && alarm.value <= t.battery_min_pct) return "ALARM ▼";
    return "OK";
  }
  return "—";
}

function getStatusBadge(status: string) {
  if (status.includes("ALARM")) return { color: "red-2", textColor: "black" };
  if (status.includes("WARNING") || status.includes("WARN"))
    return { color: "orange-2", textColor: "black" };
  if (status === "OPTIMAL") return { color: "green-2", textColor: "black" };
  return { color: "grey-2", textColor: "black" };
}

function formatValue(value: number | null, field: string): string {
  if (value === null || Number.isNaN(value)) return "—";
  if (field === "temperature") return `${value.toFixed(1)}°C`;
  if (field === "humidity") return `${value.toFixed(1)}%`;
  if (field === "light") return `${value.toFixed(0)} lux`;
  if (field === "battery") return `${value.toFixed(0)}%`;
  return String(value);
}

function formatTs(raw: string): string {
  if (!raw) return "—";
  const d = new Date(raw);
  if (Number.isNaN(d.getTime())) return raw;
  const day = d.getDate();
  const month = d.toLocaleDateString("en-MY", { month: "short" });
  const year = d.getFullYear();
  const hours = d.getHours().toString().padStart(2, "0");
  const minutes = d.getMinutes().toString().padStart(2, "0");
  return `${day} ${month} ${year} ${hours}:${minutes}`;
}

</script>

<template>
  <q-table
    :rows="sensorReadings"
    :columns="columns"
    row-key="id"
    flat
    separator="cell"
    :loading="props.loading"
    v-model:pagination="pagination"
    :rows-per-page-options="[10, 20, 50, 100]"
    class="sensor-data-table full-width"
    @mouseover="handleMouseOver"
    @mouseleave="handleMouseLeave"
  >
    <!-- Time Column -->
    <template #body-cell-ts="slotProps">
      <q-td
        auto-width
        :props="slotProps"
        :class="[mouseOverId === slotProps.row.id ? 'hover-highlight' : '']"
        @mouseover="mouseOverId = slotProps.row.id"
        @mouseleave="mouseOverId = ''"
      >
        <div>{{ formatTs(slotProps.row.ts) }}</div>
      </q-td>
    </template>

    <!-- Truck Name Column -->
    <template #body-cell-truckName="slotProps">
      <q-td
        auto-width
        :props="slotProps"
        :class="[mouseOverId === slotProps.row.id ? 'hover-highlight' : '']"
        @mouseover="mouseOverId = slotProps.row.id"
        @mouseleave="mouseOverId = ''"
      >
        {{ slotProps.row.truckName || "—" }}
      </q-td>
    </template>

    <!-- Device Column -->
    <template #body-cell-device="slotProps">
      <q-td
        auto-width
        :props="slotProps"
        class="text-weight-medium"
        :class="[mouseOverId === slotProps.row.id ? 'hover-highlight' : '']"
        @mouseover="mouseOverId = slotProps.row.id"
        @mouseleave="mouseOverId = ''"
      >
        {{ slotProps.row.label }}
      </q-td>
    </template>

    <!-- Temperature Column -->
    <template #body-cell-temperature="slotProps">
      <q-td
        auto-width
        :props="slotProps"
        :class="[mouseOverId === slotProps.row.id ? 'hover-highlight' : '']"
        @mouseover="mouseOverId = slotProps.row.id"
        @mouseleave="mouseOverId = ''"
      >
        <div class="row items-center no-wrap sensor-cell-gap">
          <span class="text-weight-medium">
            {{ formatValue(slotProps.row.temperature, "temperature") }}
          </span>
          <q-badge
            :color="getStatusBadge(slotProps.row.temperatureStatus).color"
            :text-color="getStatusBadge(slotProps.row.temperatureStatus).textColor"
          >
            {{ slotProps.row.temperatureStatus }}
          </q-badge>
        </div>
      </q-td>
    </template>

    <!-- Humidity Column -->
    <template #body-cell-humidity="slotProps">
      <q-td
        auto-width
        :props="slotProps"
        :class="[mouseOverId === slotProps.row.id ? 'hover-highlight' : '']"
        @mouseover="mouseOverId = slotProps.row.id"
        @mouseleave="mouseOverId = ''"
      >
        <div class="row items-center no-wrap sensor-cell-gap">
          <span class="text-weight-medium">
            {{ formatValue(slotProps.row.humidity, "humidity") }}
          </span>
          <q-badge
            :color="getStatusBadge(slotProps.row.humidityStatus).color"
            :text-color="getStatusBadge(slotProps.row.humidityStatus).textColor"
          >
            {{ slotProps.row.humidityStatus }}
          </q-badge>
        </div>
      </q-td>
    </template>

    <!-- Light Column -->
    <template #body-cell-light="slotProps">
      <q-td
        auto-width
        :props="slotProps"
        :class="[mouseOverId === slotProps.row.id ? 'hover-highlight' : '']"
        @mouseover="mouseOverId = slotProps.row.id"
        @mouseleave="mouseOverId = ''"
      >
        <div class="row items-center no-wrap sensor-cell-gap">
          <span class="text-weight-medium">{{ formatValue(slotProps.row.light, "light") }}</span>
          <q-badge
            :color="getStatusBadge(slotProps.row.lightStatus).color"
            :text-color="getStatusBadge(slotProps.row.lightStatus).textColor"
          >
            {{ slotProps.row.lightStatus }}
          </q-badge>
        </div>
      </q-td>
    </template>

    <!-- Battery Column -->
    <template #body-cell-battery="slotProps">
      <q-td
        auto-width
        :props="slotProps"
        :class="[mouseOverId === slotProps.row.id ? 'hover-highlight' : '']"
        @mouseover="mouseOverId = slotProps.row.id"
        @mouseleave="mouseOverId = ''"
      >
        <div class="row items-center no-wrap sensor-cell-gap">
          <span class="text-weight-medium">
            {{ formatValue(slotProps.row.battery, "battery") }}
          </span>
          <q-badge
            :color="getStatusBadge(slotProps.row.batteryStatus).color"
            :text-color="getStatusBadge(slotProps.row.batteryStatus).textColor"
          >
            {{ slotProps.row.batteryStatus }}
          </q-badge>
        </div>
      </q-td>
    </template>

    <!-- Activity Column -->
    <template #body-cell-overall="slotProps">
      <q-td
        auto-width
        :props="slotProps"
        :class="[mouseOverId === slotProps.row.id ? 'hover-highlight' : '']"
        @mouseover="mouseOverId = slotProps.row.id"
        @mouseleave="mouseOverId = ''"
      >
        <q-badge
          :color="connectivityBadge(props.deviceStatus).color"
          :text-color="connectivityBadge(props.deviceStatus).textColor"
          class="text-weight-bold"
        >
          <q-icon :name="connectivityBadge(props.deviceStatus).icon" size="11px" class="q-mr-xs" />
          {{ connectivityBadge(props.deviceStatus).label }}
        </q-badge>
      </q-td>
    </template>

    <!-- No Data State -->
    <template #no-data>
      <div class="text-center q-pa-xl text-grey-6">
        <q-icon name="fa-solid fa-temperature-half" size="3rem" class="q-mb-md" />
        <div class="text-h6">No sensor data available</div>
        <div class="text-caption">Try adjusting the date or check if the device is active.</div>
      </div>
    </template>
  </q-table>
</template>

<style lang="sass" scoped>
@import '../../../../style/_variables'

.sensor-data-table
  font-variant-numeric: tabular-nums

  :deep(thead tr th)
    font-weight: 700 !important
    font-size: 14px !important
    color: $primary-black !important
    text-transform: capitalize !important
    background-image: linear-gradient(0deg, $secondary-grey-2 0%, $white 100%) !important
    box-sizing: border-box !important
    border-top: 1px solid $secondary-grey-2 !important

  :deep(tbody tr td)
    padding: 8px !important
    font-size: 14px !important
    color: $primary-black !important

.sensor-cell-gap
  gap: 8px
</style>
