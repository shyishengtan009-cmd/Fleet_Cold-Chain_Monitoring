import { computed, type Ref } from "vue";
import type { HistoryRow } from "./useRtmHistory";

function arrMin(vals: number[]) {
  return vals.reduce((a, b) => (b < a ? b : a), vals[0]);
}
function arrMax(vals: number[]) {
  return vals.reduce((a, b) => (b > a ? b : a), vals[0]);
}

export function useRtmCharts(historyRows: Ref<HistoryRow[]>) {
  function baseChartOpts(yLabel: string, min?: number, max?: number) {
    return {
      responsive: true,
      maintainAspectRatio: false,
      interaction: { mode: "index" as const, intersect: false },
      plugins: {
        legend: { position: "top" as const },
        datalabels: { display: false },
        tooltip: { mode: "index" as const, intersect: false }
      },
      scales: {
        x: {
          type: "time" as const,
          time: { tooltipFormat: "dd MMM yyyy, HH:mm:ss" },
          ticks: { maxTicksLimit: 8 }
        },
        y: {
          type: "linear" as const,
          position: "left" as const,
          min,
          max,
          title: { display: true, text: yLabel }
        }
      }
    };
  }

  const chartTempHum = computed(() => {
    const temps = historyRows.value
      .map((r) => r.temperatureC)
      .filter((v): v is number => v != null);
    const hums = historyRows.value.map((r) => r.humidityPct).filter((v): v is number => v != null);
    return {
      datasets: [
        {
          label: "Temperature (°C)",
          data: historyRows.value
            .filter((r) => r.temperatureC != null)
            .map((r) => ({ x: r.ts, y: r.temperatureC as number })),
          borderColor: "rgb(220,38,38)",
          backgroundColor: "rgba(220,38,38,0.15)",
          tension: 0.4,
          pointStyle: false as const,
          pointHoverRadius: 5,
          yAxisID: "yLeft"
        },
        {
          label: "Humidity (%)",
          data: historyRows.value
            .filter((r) => r.humidityPct != null)
            .map((r) => ({ x: r.ts, y: r.humidityPct as number })),
          borderColor: "rgb(54,162,235)",
          backgroundColor: "rgba(54,162,235,0.15)",
          tension: 0.4,
          pointStyle: false as const,
          pointHoverRadius: 5,
          yAxisID: "yRight"
        }
      ],
      _tRange: temps.length
        ? [Math.floor(arrMin(temps) - 2), Math.ceil(arrMax(temps) + 2)]
        : [0, 50],
      _hRange: hums.length ? [Math.floor(arrMin(hums) - 2), Math.ceil(arrMax(hums) + 2)] : [0, 100]
    };
  });

  const chartTempHumOpts = computed(() => ({
    responsive: true,
    maintainAspectRatio: false,
    interaction: { mode: "index" as const, intersect: false },
    plugins: { legend: { position: "top" as const }, datalabels: { display: false } },
    scales: {
      x: {
        type: "time" as const,
        time: { tooltipFormat: "dd MMM yyyy, HH:mm:ss" },
        ticks: { maxTicksLimit: 8 }
      },
      yLeft: {
        type: "linear" as const,
        position: "left" as const,
        min: chartTempHum.value._tRange[0],
        max: chartTempHum.value._tRange[1],
        title: { display: true, text: "Temperature (°C)", color: "rgb(220,38,38)" }
      },
      yRight: {
        type: "linear" as const,
        position: "right" as const,
        min: chartTempHum.value._hRange[0],
        max: chartTempHum.value._hRange[1],
        title: { display: true, text: "Humidity (%)", color: "rgb(54,162,235)" },
        grid: { drawOnChartArea: false }
      }
    }
  }));

  const chartLight = computed(() => ({
    datasets: [
      {
        label: "Light (lux)",
        data: historyRows.value
          .filter((r) => r.lightLux != null)
          .map((r) => ({ x: r.ts, y: r.lightLux as number })),
        borderColor: "rgb(34,197,94)",
        backgroundColor: "rgba(34,197,94,0.15)",
        tension: 0.4,
        pointStyle: false as const,
        pointHoverRadius: 5
      }
    ]
  }));

  const chartLightOpts = computed(() => {
    const vals = historyRows.value.map((r) => r.lightLux).filter((v): v is number => v != null);
    return baseChartOpts(
      "Light (lux)",
      vals.length ? Math.floor(arrMin(vals) - 1) : 0,
      vals.length ? Math.ceil(arrMax(vals) + 1) : 1000
    );
  });

  const chartBattery = computed(() => ({
    datasets: [
      {
        label: "Battery (%)",
        data: historyRows.value
          .filter((r) => r.batteryPct != null)
          .map((r) => ({ x: r.ts, y: r.batteryPct as number })),
        borderColor: "rgb(99,102,241)",
        backgroundColor: "rgba(99,102,241,0.15)",
        tension: 0.4,
        pointStyle: false as const,
        pointHoverRadius: 5
      }
    ]
  }));

  const chartBatteryOpts = baseChartOpts("Battery (%)", 0, 100);

  const hasVibration = computed(() => historyRows.value.some((r) => r.vibrationG != null));

  const chartVibration = computed(() => ({
    datasets: [
      {
        label: "Vibration (g)",
        data: historyRows.value
          .filter((r) => r.vibrationG != null)
          .map((r) => ({ x: r.ts, y: r.vibrationG as number })),
        borderColor: "rgb(251,146,60)",
        backgroundColor: "rgba(251,146,60,0.15)",
        tension: 0.4,
        pointStyle: false as const,
        pointHoverRadius: 5
      }
    ]
  }));

  const chartVibrationOpts = computed(() => {
    const vals = historyRows.value.map((r) => r.vibrationG).filter((v): v is number => v != null);
    const rawMin = vals.length ? arrMin(vals) : 0;
    const rawMax = vals.length ? arrMax(vals) : 1;
    const range = rawMax - rawMin;
    const yMin = range < 0.5 ? rawMin - 0.5 : Math.floor(rawMin * 10) / 10;
    const yMax = range < 0.5 ? rawMax + 0.5 : Math.ceil(rawMax * 10) / 10 + 0.1;
    return baseChartOpts("Vibration (g)", yMin, yMax);
  });

  const hasRssi = computed(() => historyRows.value.some((r) => r.rssiDbm != null));

  const chartRssi = computed(() => ({
    datasets: [
      {
        label: "RSSI (dBm)",
        data: historyRows.value
          .filter((r) => r.rssiDbm != null)
          .map((r) => ({ x: r.ts, y: r.rssiDbm as number })),
        borderColor: "rgb(139,92,246)",
        backgroundColor: "rgba(139,92,246,0.15)",
        tension: 0.4,
        pointStyle: false as const,
        pointHoverRadius: 5
      }
    ]
  }));

  const chartRssiOpts = computed(() => {
    const vals = historyRows.value.map((r) => r.rssiDbm).filter((v): v is number => v != null);
    return baseChartOpts(
      "RSSI (dBm)",
      vals.length ? Math.floor(arrMin(vals) - 2) : -120,
      vals.length ? Math.ceil(arrMax(vals) + 2) : 0
    );
  });

  return {
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
  };
}
