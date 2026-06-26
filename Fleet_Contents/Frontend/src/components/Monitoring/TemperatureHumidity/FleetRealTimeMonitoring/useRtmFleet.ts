import { ref, computed } from "vue";
import api from "@/helpers/api";
import { useFleetDeviceSelection } from "../Fleet/useFleetDeviceSelection";

export interface FleetItem {
  status: "OK" | "WARN" | "OFFLINE";
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

export function useRtmFleet() {
  const { selectedHardwareId: selectedId, setDevice } = useFleetDeviceSelection();
  const fleetItems = ref<FleetItem[]>([]);
  const fleetLoading = ref(false);
  const fleetError = ref<string | null>(null);
  const fleetUpdated = ref<string | null>(null);
  const refreshSec = ref(30);
  const warnMin = ref(15);
  const offlineMin = ref(30);

  let pollTimer: ReturnType<typeof setInterval> | null = null;

  const selectedItem = computed(
    () => fleetItems.value.find((i) => i.hardwareId === selectedId.value) ?? null
  );

  const statusCounts = computed(() => ({
    ok: fleetItems.value.filter((i) => i.status === "OK").length,
    warn: fleetItems.value.filter((i) => i.status === "WARN").length,
    offline: fleetItems.value.filter((i) => i.status === "OFFLINE").length
  }));

  const columns = [
    { name: "status", label: "Status", field: "status", align: "left" as const, sortable: true },
    {
      name: "hardwareId",
      label: "Hardware ID",
      field: "hardwareId",
      align: "left" as const,
      sortable: true
    },
    {
      name: "truckName",
      label: "Truck",
      field: "truckName",
      align: "left" as const,
      sortable: true
    },
    { name: "plate", label: "Plate", field: "plate", align: "left" as const, sortable: true },
    {
      name: "temperatureC",
      label: "Temp (°C)",
      field: "temperatureC",
      align: "right" as const,
      sortable: true
    },
    {
      name: "humidityPct",
      label: "Humidity (%)",
      field: "humidityPct",
      align: "right" as const,
      sortable: true
    },
    {
      name: "lightLux",
      label: "Light (lux)",
      field: "lightLux",
      align: "right" as const,
      sortable: true
    },
    {
      name: "batteryPct",
      label: "Battery (%)",
      field: "batteryPct",
      align: "right" as const,
      sortable: true
    },
    {
      name: "ageSeconds",
      label: "Age (min)",
      field: "ageSeconds",
      align: "right" as const,
      sortable: true
    }
  ];

  async function fetchFleet() {
    fleetLoading.value = true;
    fleetError.value = null;
    try {
      const result = await api.fleet.getFleetStatus({
        warn_seconds: warnMin.value * 60,
        offline_seconds: offlineMin.value * 60,
        limit: 1000
      });
      const raw = result as Record<string, unknown>;
      const details = raw?.details;
      const data = raw?.data;
      let items: Record<string, unknown>[] = [];
      if (Array.isArray(raw)) items = raw;
      else if (Array.isArray(details)) items = details as Record<string, unknown>[];
      else if (
        details &&
        typeof details === "object" &&
        Array.isArray((details as Record<string, unknown>).items)
      )
        items = (details as { items: Record<string, unknown>[] }).items;
      else if (Array.isArray(data)) items = data as Record<string, unknown>[];
      else if (
        data &&
        typeof data === "object" &&
        Array.isArray((data as Record<string, unknown>).items)
      )
        items = (data as { items: Record<string, unknown>[] }).items;
      else if (Array.isArray(raw?.items)) items = raw.items as Record<string, unknown>[];
      const get = (r: Record<string, unknown>, camel: string, snake: string) =>
        r[camel] ?? r[snake];
      fleetItems.value = items.map((r): FleetItem => {
        const row = r as Record<string, unknown>;
        return {
          status: (get(row, "status", "status") as "OK" | "WARN" | "OFFLINE") ?? "OFFLINE",
          hardwareId: (get(row, "hardwareId", "hardware_id") as string) ?? "",
          temperatureC: (get(row, "temperatureC", "temperature_c") as number | null) ?? null,
          humidityPct: (get(row, "humidityPct", "humidity_pct") as number | null) ?? null,
          lightLux: (get(row, "lightLux", "light_lux") as number | null) ?? null,
          batteryPct: (get(row, "batteryPct", "battery_pct") as number | null) ?? null,
          ts: (get(row, "ts", "ts") as string | null) ?? null,
          ageSeconds: (get(row, "ageSeconds", "age_seconds") as number | null) ?? null,
          truckId: (get(row, "truckId", "truck_id") as number | null) ?? null,
          truckName: (get(row, "truckName", "truck_name") as string | null) ?? null,
          plate: (get(row, "plate", "plate") as string | null) ?? null,
          sensorName: (get(row, "sensorName", "sensor_name") as string | null) ?? null
        };
      });
      fleetUpdated.value = new Date().toLocaleTimeString("en-GB");
      const first = fleetItems.value[0];
      if (first?.hardwareId) {
        const exists = fleetItems.value.some((i) => i.hardwareId === selectedId.value);
        if (!selectedId.value || !exists) setDevice(first.hardwareId);
      }
    } catch (e: unknown) {
      fleetError.value = String(e);
    } finally {
      fleetLoading.value = false;
    }
  }

  function selectDevice(hw: string) {
    setDevice(hw);
  }

  function resetPoll(onTick: () => void) {
    if (pollTimer) clearInterval(pollTimer);
    pollTimer = setInterval(onTick, refreshSec.value * 1000);
  }

  function stopPoll() {
    if (pollTimer) clearInterval(pollTimer);
  }

  function applySettings(onTick: () => void) {
    resetPoll(onTick);
    fetchFleet();
  }

  return {
    fleetItems,
    fleetLoading,
    fleetError,
    fleetUpdated,
    selectedId,
    selectedItem,
    statusCounts,
    columns,
    refreshSec,
    warnMin,
    offlineMin,
    fetchFleet,
    selectDevice,
    resetPoll,
    stopPoll,
    applySettings
  };
}
