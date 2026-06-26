import { ref } from "vue";
import api from "@/helpers/api";

export interface HistoryRow {
  ts: string;
  temperatureC: number | null;
  humidityPct: number | null;
  lightLux: number | null;
  batteryPct: number | null;
  vibrationG: number | null;
  rssiDbm: number | null;
  latLng: string | null;
  raw: Record<string, unknown> | null;
}

const MAX_ROWS = 10_000;

export function useRtmHistory() {
  const historyRows = ref<HistoryRow[]>([]);
  const historyLoading = ref(false);
  const historyError = ref<string | null>(null);

  let lastKnownTs: string | null = null;
  let currentHw: string | null = null;

  function parseRows(rangeRaw: Record<string, unknown>): Record<string, unknown>[] {
    const rangeDetails = rangeRaw?.details as Record<string, unknown> | undefined;
    const rangeData = rangeRaw?.data as Record<string, unknown> | undefined;
    const rangePayload = rangeDetails ?? rangeData ?? rangeRaw;
    if (Array.isArray(rangePayload)) return rangePayload;
    if (Array.isArray(rangePayload?.rows)) return rangePayload.rows as Record<string, unknown>[];
    if (Array.isArray(rangeDetails?.rows)) return rangeDetails!.rows as Record<string, unknown>[];
    if (Array.isArray(rangeRaw?.rows)) return rangeRaw.rows as Record<string, unknown>[];
    return [];
  }

  function mapRow(r: Record<string, unknown>): HistoryRow {
    const raw = (r.raw ?? r) as Record<string, unknown> | null;
    const g = (o: Record<string, unknown>, a: string, b: string) => o[a] ?? o[b];
    const vibrationG =
      raw != null
        ? ((g(raw, "vibration_g", "vibration") ?? g(raw, "vibrationG", "vibration")) as
            | number
            | null)
        : null;
    const rssiDbm =
      raw != null
        ? ((g(raw, "rssi_dbm", "rssi") ?? g(raw, "rssiDbm", "rssi")) as number | null)
        : null;
    return {
      ts: (g(r, "ts", "ts") as string) ?? "",
      temperatureC: (g(r, "temperatureC", "temperature_c") as number | null) ?? null,
      humidityPct: (g(r, "humidityPct", "humidity_pct") as number | null) ?? null,
      lightLux: (g(r, "lightLux", "light_lux") as number | null) ?? null,
      batteryPct: (g(r, "batteryPct", "battery_pct") as number | null) ?? null,
      vibrationG,
      rssiDbm,
      latLng: (raw?.latLng as string | null) ?? (raw?.lat_lng as string | null) ?? null,
      raw
    };
  }

  async function fetchHistory(hw: string) {
    // Reset state when switching to a different device
    if (hw !== currentHw) {
      currentHw = hw;
      lastKnownTs = null;
      historyRows.value = [];
    }

    historyLoading.value = true;
    historyError.value = null;
    try {
      const isInitial = lastKnownTs === null;
      let start: string;
      let end: string;
      if (isInitial) {
        // A device that hasn't reported in the last 7 days would otherwise return
        // zero rows from a "now - 7 days" window forever. Anchor the initial window
        // to the device's actual latest reading when it's older than that, so RTM
        // shows the device's real last-known data instead of a permanently empty chart.
        const sevenDaysAgo = Date.now() - 7 * 24 * 3_600_000;
        let windowEnd = Date.now() + 60_000;
        try {
          const meta = (await api.fleet.getHistoryMeta(hw)) as unknown as Record<string, unknown>;
          const details = (meta?.details ?? meta) as Record<string, unknown>;
          const maxTs = (details?.maxTs ?? details?.max_ts) as string | null | undefined;
          if ((details?.found ?? details?.Found) && maxTs) {
            const maxTime = new Date(maxTs).getTime();
            if (!isNaN(maxTime) && maxTime < sevenDaysAgo) windowEnd = maxTime + 60_000;
          }
        } catch {
          // fall through to the standard "now" window
        }
        start = new Date(windowEnd - 7 * 24 * 3_600_000).toISOString();
        end = new Date(windowEnd).toISOString();
      } else {
        start = lastKnownTs!;
        // Extend end 60 s into the future to capture rows that arrived at the server
        // just after the previous client snapshot (absorbs typical clock skew).
        end = new Date(Date.now() + 60_000).toISOString();
      }

      const rangeResult = await api.fleet.getHistoryRange(hw, start, end);
      const rows = parseRows(rangeResult as unknown as Record<string, unknown>);

      if (rows.length === 0) return;

      const tsOf = (r: Record<string, unknown>) => String(r.ts ?? r.timestamp ?? "");
      const sorted = [...rows].sort(
        (a, b) => new Date(tsOf(a)).getTime() - new Date(tsOf(b)).getTime()
      );
      const mapped = sorted.map(mapRow);

      if (isInitial) {
        historyRows.value = mapped;
      } else {
        // Append incremental rows, cap total to avoid unbounded growth
        historyRows.value = [...historyRows.value, ...mapped].slice(-MAX_ROWS);
      }

      // Advance cursor 1 ms past the latest row so the next range query (ts >= cursor)
      // is exclusive of the row we just processed, preventing a duplicate on the next poll.
      const latestTs = tsOf(sorted[sorted.length - 1]);
      if (latestTs) lastKnownTs = new Date(new Date(latestTs).getTime() + 1).toISOString();
    } catch (e: unknown) {
      historyError.value = String(e);
    } finally {
      historyLoading.value = false;
    }
  }

  return { historyRows, historyLoading, historyError, fetchHistory };
}
