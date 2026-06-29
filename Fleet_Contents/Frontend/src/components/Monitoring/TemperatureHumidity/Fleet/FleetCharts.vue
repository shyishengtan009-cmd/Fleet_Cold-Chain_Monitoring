<script setup lang="ts">
import {
  CategoryScale,
  Chart as ChartJS,
  Legend,
  LineController,
  LineElement,
  LinearScale,
  PointElement,
  TimeScale,
  Title,
  Tooltip
} from "chart.js";
import "chartjs-adapter-date-fns";
import { computed } from "vue";
import { Line } from "vue-chartjs";

ChartJS.register(
  Title,
  Tooltip,
  Legend,
  LineElement,
  LineController,
  CategoryScale,
  LinearScale,
  PointElement,
  TimeScale
);

// Crosshair plugin — draws a vertical line at the hovered x position
const crosshairPlugin = {
  id: "crosshair",
  afterDraw(chart: ChartJS) {
    const active = chart.tooltip?.getActiveElements();
    if (!active || active.length === 0) return;
    const ctx = chart.ctx;
    const x = active[0].element.x;
    const yAxis = chart.scales["y"] ?? chart.scales["yLeft"];
    if (!yAxis) return;
    ctx.save();
    ctx.beginPath();
    ctx.moveTo(x, yAxis.top);
    ctx.lineTo(x, yAxis.bottom);
    ctx.lineWidth = 1;
    ctx.strokeStyle = "rgba(100,100,100,0.4)";
    ctx.setLineDash([4, 4]);
    ctx.stroke();
    ctx.restore();
  }
};
ChartJS.register(crosshairPlugin);

export interface HistoryRow {
  ts: string;
  temperatureC: number | null;
  humidityPct: number | null;
  lightLux: number | null;
  batteryPct: number | null;
  vibrationG: number | null;
  rssi: number | null;
}

export interface AggregatedRow {
  ts: string;
  temp_min: number | null;
  temp_max: number | null;
  temp_avg: number | null;
  hum_min: number | null;
  hum_max: number | null;
  hum_avg: number | null;
  light_min: number | null;
  light_max: number | null;
  light_avg: number | null;
  batt_min: number | null;
  batt_max: number | null;
  batt_avg: number | null;
}

const props = defineProps<{
  historyRows: HistoryRow[];
  aggregatedRows?: AggregatedRow[];
}>();

// Downsample raw rows to at most MAX_CHART_POINTS to keep Chart.js responsive.
// For ranges > 24 h the parent passes aggregatedRows instead; this path only
// fires when the caller still provides raw rows for a short range.
const MAX_CHART_POINTS = 300;

const displayRows = computed<HistoryRow[]>(() => {
  const rows = props.historyRows;
  if (rows.length <= MAX_CHART_POINTS) return rows;
  const step = Math.ceil(rows.length / MAX_CHART_POINTS);
  return rows.filter((_, i) => i % step === 0);
});

const isAggregated = computed(() => (props.aggregatedRows?.length ?? 0) > 0);

// ─── Chart Data ───────────────────────────────────────────────────────────────

const chartTempHum = computed(() => {
  if (isAggregated.value) {
    const agg = props.aggregatedRows!;
    return {
      datasets: [
        {
          label: "Temp avg (°C)",
          data: agg.filter((r) => r.temp_avg != null).map((r) => ({ x: r.ts, y: r.temp_avg as number })),
          borderColor: "rgb(220, 38, 38)",
          backgroundColor: "rgba(220, 38, 38, 0.15)",
          tension: 0.3,
          pointStyle: false as const,
          pointHoverRadius: 4,
          pointHoverBackgroundColor: "rgb(220, 38, 38)",
          yAxisID: "yLeft"
        },
        {
          label: "Hum avg (%)",
          data: agg.filter((r) => r.hum_avg != null).map((r) => ({ x: r.ts, y: r.hum_avg as number })),
          borderColor: "rgb(54, 162, 235)",
          backgroundColor: "rgba(54, 162, 235, 0.15)",
          tension: 0.3,
          pointStyle: false as const,
          pointHoverRadius: 4,
          pointHoverBackgroundColor: "rgb(54, 162, 235)",
          yAxisID: "yRight"
        }
      ]
    };
  }
  return ({
  datasets: [
    {
      label: "Temperature (°C)",
      data: displayRows.value
        .filter((r) => r.temperatureC != null)
        .map((r) => ({ x: r.ts, y: r.temperatureC as number })),
      borderColor: "rgb(220, 38, 38)",
      backgroundColor: "rgba(220, 38, 38, 0.2)",
      tension: 0.4,
      pointStyle: false as const,
      pointHoverRadius: 5,
      pointHoverBackgroundColor: "rgb(220, 38, 38)",
      yAxisID: "yLeft"
    },
    {
      label: "Humidity (%)",
      data: displayRows.value
        .filter((r) => r.humidityPct != null)
        .map((r) => ({ x: r.ts, y: r.humidityPct as number })),
      borderColor: "rgb(54, 162, 235)",
      backgroundColor: "rgba(54, 162, 235, 0.2)",
      tension: 0.4,
      pointStyle: false as const,
      pointHoverRadius: 5,
      pointHoverBackgroundColor: "rgb(54, 162, 235)",
      yAxisID: "yRight"
    }
  ]
  });
});

const chartLight = computed(() => ({
  datasets: [
    {
      label: "Light (lux)",
      data: displayRows.value
        .filter((r) => r.lightLux != null)
        .map((r) => ({ x: r.ts, y: r.lightLux as number })),
      borderColor: "rgb(34, 197, 94)",
      backgroundColor: "rgba(34, 197, 94, 0.2)",
      tension: 0.4,
      pointStyle: false as const,
      pointHoverRadius: 5,
      pointHoverBackgroundColor: "rgb(34, 197, 94)"
    }
  ]
}));

const chartBattery = computed(() => ({
  datasets: [
    {
      label: "Battery (%)",
      data: displayRows.value
        .filter((r) => r.batteryPct != null)
        .map((r) => ({ x: r.ts, y: r.batteryPct as number })),
      borderColor: "rgb(99, 102, 241)",
      backgroundColor: "rgba(99, 102, 241, 0.2)",
      tension: 0.4,
      pointStyle: false as const,
      pointHoverRadius: 5,
      pointHoverBackgroundColor: "rgb(99, 102, 241)"
    }
  ]
}));

const hasVibration = computed(() =>
  displayRows.value.some((r) => r.vibrationG !== null && r.vibrationG !== undefined)
);

const chartRssi = computed(() => ({
  datasets: [
    {
      label: "RSSI (dBm)",
      data: displayRows.value
        .filter((r) => r.rssi !== null && r.rssi !== undefined)
        .map((r) => ({ x: r.ts, y: r.rssi as number })),
      borderColor: "rgb(168, 85, 247)",
      backgroundColor: "rgba(168, 85, 247, 0.2)",
      tension: 0.4,
      pointStyle: false as const,
      pointHoverRadius: 5,
      pointHoverBackgroundColor: "rgb(168, 85, 247)"
    }
  ]
}));

const chartOptionsRssi = computed(() => {
  const vals = displayRows.value.map((r) => r.rssi).filter((v): v is number => v != null);
  const yMin = vals.length ? Math.floor(Math.min(...vals) - 2) : -120;
  const yMax = vals.length ? Math.ceil(Math.max(...vals) + 2) : 0;
  return {
    responsive: true,
    maintainAspectRatio: false,
    interaction: { mode: "index" as const, intersect: false },
    plugins: basePlugins,
    scales: {
      x: baseScaleX,
      y: {
        type: "linear" as const,
        position: "left" as const,
        min: yMin,
        max: yMax,
        title: { display: true, text: "RSSI (dBm)" }
      }
    }
  };
});

const hasRssi = computed(() =>
  displayRows.value.some((r) => r.rssi !== null && r.rssi !== undefined)
);

const chartVibration = computed(() => ({
  datasets: [
    {
      label: "Vibration (g)",
      data: displayRows.value
        .filter((r) => r.vibrationG !== null && r.vibrationG !== undefined)
        .map((r) => ({ x: r.ts, y: r.vibrationG as number })),
      borderColor: "rgb(251, 146, 60)",
      backgroundColor: "rgba(251, 146, 60, 0.2)",
      tension: 0.4,
      pointStyle: false as const,
      pointHoverRadius: 5,
      pointHoverBackgroundColor: "rgb(251, 146, 60)"
    }
  ]
}));

// ─── Chart Options ────────────────────────────────────────────────────────────

const baseScaleX = {
  type: "time" as const,
  time: { tooltipFormat: "dd MMM yyyy, HH:mm:ss" },
  ticks: { maxTicksLimit: 8 }
};

const basePlugins = {
  legend: { position: "top" as const },
  datalabels: { display: false },
  tooltip: {
    mode: "index" as const,
    intersect: false,
    callbacks: {
      title: (items: { label: string }[]) => items[0]?.label ?? ""
    }
  }
};

const chartOptionsDual = computed(() => {
  const src = isAggregated.value
    ? props.aggregatedRows!.map((r) => ({ temperatureC: r.temp_avg, humidityPct: r.hum_avg }))
    : displayRows.value;
  const temps = src.map((r) => r.temperatureC).filter((v): v is number => v != null);
  const hums  = src.map((r) => r.humidityPct).filter((v): v is number => v != null);
  const tMin = temps.length ? Math.floor(Math.min(...temps) - 2) : 0;
  const tMax = temps.length ? Math.ceil(Math.max(...temps) + 2) : 50;
  const hMin = hums.length ? Math.floor(Math.min(...hums) - 2) : 0;
  const hMax = hums.length ? Math.ceil(Math.max(...hums) + 2) : 100;

  return {
    responsive: true,
    maintainAspectRatio: false,
    interaction: { mode: "index" as const, intersect: false },
    plugins: basePlugins,
    scales: {
      x: baseScaleX,
      yLeft: {
        type: "linear" as const,
        position: "left" as const,
        min: tMin,
        max: tMax,
        title: { display: true, text: "Temperature (°C)", color: "rgb(220, 38, 38)" }
      },
      yRight: {
        type: "linear" as const,
        position: "right" as const,
        min: hMin,
        max: hMax,
        title: { display: true, text: "Humidity (%)", color: "rgb(54, 162, 235)" },
        grid: { drawOnChartArea: false }
      }
    }
  };
});

const chartOptionsLight = computed(() => {
  const vals = displayRows.value.map((r) => r.lightLux).filter((v): v is number => v != null);
  const yMin = vals.length ? Math.floor(Math.min(...vals) - 1) : 0;
  const yMax = vals.length ? Math.ceil(Math.max(...vals) + 1) : 1000;
  return {
    responsive: true,
    maintainAspectRatio: false,
    interaction: { mode: "index" as const, intersect: false },
    plugins: basePlugins,
    scales: {
      x: baseScaleX,
      y: { type: "linear" as const, position: "left" as const, min: yMin, max: yMax }
    }
  };
});

const chartOptionsBattery = {
  responsive: true,
  maintainAspectRatio: false,
  interaction: { mode: "index" as const, intersect: false },
  plugins: basePlugins,
  scales: {
    x: baseScaleX,
    y: {
      type: "linear" as const,
      position: "left" as const,
      min: 0,
      max: 100,
      title: { display: true, text: "Battery (%)" }
    }
  }
};

const chartOptionsVibration = computed(() => {
  const vals = displayRows.value.map((r) => r.vibrationG).filter((v): v is number => v != null);
  const rawMin = vals.length ? Math.min(...vals) : 0;
  const rawMax = vals.length ? Math.max(...vals) : 1;
  const range = rawMax - rawMin;
  const yMin = range < 0.5 ? rawMin - 0.5 : Math.floor(rawMin * 10) / 10;
  const yMax = range < 0.5 ? rawMax + 0.5 : Math.ceil(rawMax * 10) / 10 + 0.1;
  return {
    responsive: true,
    maintainAspectRatio: false,
    interaction: { mode: "index" as const, intersect: false },
    plugins: basePlugins,
    scales: {
      x: baseScaleX,
      y: { type: "linear" as const, position: "left" as const, min: yMin, max: yMax }
    }
  };
});
</script>

<template>
  <div
    style="
      width: 420px;
      min-width: 360px;
      flex-shrink: 0;
      display: flex;
      flex-direction: column;
      align-self: stretch;
    "
  >
    <div style="display: flex; flex-direction: column; flex: 1; gap: 2px; padding: 2px">
      <!-- Temperature & Humidity -->
      <div :class="$style.chartCard">
        <div :class="$style.chartTitle">Temperature & Humidity</div>
        <div :class="$style.chartWrap">
          <Line v-if="displayRows.length || isAggregated" :data="chartTempHum" :options="chartOptionsDual" />
          <div v-else :class="$style.noData">No data</div>
        </div>
      </div>

      <!-- Light -->
      <div :class="$style.chartCard">
        <div :class="$style.chartTitle">Light</div>
        <div :class="$style.chartWrap">
          <Line v-if="displayRows.length || isAggregated" :data="chartLight" :options="chartOptionsLight" />
          <div v-else :class="$style.noData">No data</div>
        </div>
      </div>

      <!-- Battery -->
      <div :class="$style.chartCard">
        <div :class="$style.chartTitle">Battery</div>
        <div :class="$style.chartWrap">
          <Line v-if="displayRows.length || isAggregated" :data="chartBattery" :options="chartOptionsBattery" />
          <div v-else :class="$style.noData">No data</div>
        </div>
      </div>

      <!-- Vibration -->
      <div :class="$style.chartCard">
        <div :class="$style.chartTitle">Vibration</div>
        <div :class="$style.chartWrap">
          <Line v-if="hasVibration" :data="chartVibration" :options="chartOptionsVibration" />
          <div v-else :class="$style.noData">No vibration data in raw</div>
        </div>
      </div>

      <!-- RSSI -->
      <div :class="$style.chartCard">
        <div :class="$style.chartTitle">RSSI</div>
        <div :class="$style.chartWrap">
          <Line v-if="hasRssi" :data="chartRssi" :options="chartOptionsRssi" />
          <div v-else :class="$style.noData">No RSSI data</div>
        </div>
      </div>
    </div>
  </div>
</template>

<style lang="sass" module>
@import '../../../../style/_variables'

.chartCard
  border: 1px solid $secondary-grey-2
  border-radius: 4px
  padding: 4px 6px
  background: $white
  flex: 1
  display: flex
  flex-direction: column
  min-height: 0

.chartTitle
  font-weight: 700
  font-size: $font-size-xs
  color: $primary-black
  margin-bottom: 2px
  flex-shrink: 0

.chartWrap
  position: relative
  flex: 1
  min-height: 0
  cursor: crosshair

.noData
  display: flex
  align-items: center
  justify-content: center
  height: 100%
  color: $secondary-grey-1
  font-size: $font-size-sm
</style>
