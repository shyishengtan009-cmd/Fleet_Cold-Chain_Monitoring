import { computed, type Ref } from "vue";
import type { FleetItem } from "./useRtmFleet";
import type { AlarmThresholds } from "./useRtmSettings";
import { defaultThresholds } from "./useRtmSettings";

export function useRtmAlarms(
  selectedItem: Ref<FleetItem | null>,
  thresholds: Ref<AlarmThresholds>
) {
  const alarms = computed(() => {
    if (!selectedItem.value) return [];
    const out: { field: string; value: string; level: string }[] = [];
    const s = selectedItem.value;
    const t = thresholds.value ?? defaultThresholds;

    const {
      temp_min_c,
      temp_max_c,
      humidity_min_pct,
      humidity_max_pct,
      light_min_lux,
      light_max_lux,
      vibration_g,
      battery_min_pct
    } = t;

    if (s.temperatureC != null) {
      if (temp_max_c != null && s.temperatureC > temp_max_c)
        out.push({ field: "Temperature", value: `${s.temperatureC.toFixed(1)}°C`, level: "ALARM" });
      else if (temp_min_c != null && s.temperatureC < temp_min_c)
        out.push({ field: "Temperature", value: `${s.temperatureC.toFixed(1)}°C`, level: "ALARM" });
    }

    if (s.humidityPct != null) {
      if (humidity_max_pct != null && s.humidityPct > humidity_max_pct)
        out.push({ field: "Humidity", value: `${s.humidityPct.toFixed(1)}%`, level: "WARN" });
      else if (humidity_min_pct != null && s.humidityPct < humidity_min_pct)
        out.push({ field: "Humidity", value: `${s.humidityPct.toFixed(1)}%`, level: "WARN" });
    }

    if (s.lightLux != null) {
      if (light_max_lux != null && s.lightLux > light_max_lux)
        out.push({ field: "Light", value: `${s.lightLux.toFixed(0)} lux`, level: "WARN" });
      else if (light_min_lux != null && s.lightLux < light_min_lux)
        out.push({ field: "Light", value: `${s.lightLux.toFixed(0)} lux`, level: "WARN" });
    }

    if (s.batteryPct != null && battery_min_pct != null && s.batteryPct < battery_min_pct)
      out.push({ field: "Battery", value: `${s.batteryPct}%`, level: "ALARM" });

    return out;
  });

  return { alarms };
}
