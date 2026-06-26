import { ref, computed, onMounted } from "vue";
import * as XLSX from "xlsx";
import api from "@/helpers/api";
import { extractLatLng } from "./useRtmMap";

export interface TripOption {
  tripId: number;
  label: string;
  selected: boolean;
}

function isoToLocalInput(iso: string | null): string {
  if (!iso) return "";
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function localInputToIso(val: string): string {
  return val ? new Date(val).toISOString() : "";
}

function todayRange() {
  const now = new Date();
  const y = now.getFullYear(),
    mo = now.getMonth(),
    d = now.getDate();
  return {
    start: new Date(y, mo, d, 0, 0).toISOString(),
    end: new Date(y, mo, d, 23, 59).toISOString()
  };
}

function alignLeft(ws: XLSX.WorkSheet): void {
  const ref = ws["!ref"];
  if (!ref) return;
  const range = XLSX.utils.decode_range(ref);
  for (let R = range.s.r; R <= range.e.r; R++) {
    for (let C = range.s.c; C <= range.e.c; C++) {
      const addr = XLSX.utils.encode_cell({ r: R, c: C });
      const cell = ws[addr];
      if (cell && cell.t === "n") {
        cell.v = cell.w ?? String(cell.v);
        cell.t = "s";
        delete cell.w;
      }
    }
  }
}

export function useFleetExport(hardwareId: () => string) {
  const exportSensor = ref(true);
  const sensorStart = ref("");
  const sensorEnd = ref("");

  const exportTrips = ref(true);
  const tripOptions = ref<TripOption[]>([]);
  const tripsLoading = ref(false);
  const anyTripSelected = computed(() => tripOptions.value.some((t) => t.selected));

  const exportShipment = ref(true);

  const exportAlarms = ref(true);
  const alarmStart = ref("");
  const alarmEnd = ref("");

  const exporting = ref(false);
  const error = ref<string | null>(null);
  const step = ref<"config" | "done">("config");

  async function loadTrips() {
    tripsLoading.value = true;
    try {
      const result = await api.fleet.getTrips(hardwareId(), 100);
      const data = ((result as any).details ?? result) as { trips: Record<string, unknown>[] };
      tripOptions.value = (data?.trips ?? []).map((t) => {
        const start = t.start_time ? new Date(t.start_time as string).toLocaleString() : "?";
        const end = t.end_time ? new Date(t.end_time as string).toLocaleString() : "In Progress";
        const km = t.total_distance_km ? `${Number(t.total_distance_km).toFixed(1)} km` : "";
        const minsLate = t.minutes_late != null ? Number(t.minutes_late) : null;
        const slaFlag =
          minsLate != null ? (minsLate > 0 ? ` ⚠ LATE +${minsLate}m` : " ✓ On Time") : "";
        const hotBreach =
          t.breach_minutes_hot != null && Number(t.breach_minutes_hot) > 0
            ? `🌡+${t.breach_minutes_hot}m`
            : "";
        const coldBreach =
          t.breach_minutes_cold != null && Number(t.breach_minutes_cold) > 0
            ? `🌡-${t.breach_minutes_cold}m`
            : "";
        const breach = [hotBreach, coldBreach].filter(Boolean).join(" ");
        const extras = [km, slaFlag, breach].filter(Boolean).join("  •  ");
        return {
          tripId: Number(t.trip_id),
          label: `Trip #${t.trip_id} — ${start} → ${end}${extras ? "  |  " + extras : ""}`,
          selected: true
        };
      });
    } catch {
      /* silently ignore */
    } finally {
      tripsLoading.value = false;
    }
  }

  onMounted(async () => {
    const { start, end } = todayRange();
    sensorStart.value = isoToLocalInput(start);
    sensorEnd.value = isoToLocalInput(end);
    alarmStart.value = isoToLocalInput(start);
    alarmEnd.value = isoToLocalInput(end);
    await loadTrips();
  });

  async function runExport() {
    error.value = null;
    exporting.value = true;
    try {
      const wb = XLSX.utils.book_new();
      const hw = hardwareId();

      if (exportSensor.value) {
        const startIso = localInputToIso(sensorStart.value);
        const endIso = localInputToIso(sensorEnd.value);
        if (!startIso || !endIso) throw new Error("Please set a valid date range for Sensor Data.");
        if (new Date(startIso) >= new Date(endIso))
          throw new Error("Sensor Data start must be before end.");

        const result = await api.fleet.getHistoryRange(hw, startIso, endIso, 5000);
        const data = ((result as any).details ?? result) as { rows: Record<string, unknown>[] };
        const rows = (data?.rows ?? []).sort(
          (a, b) => new Date(a.ts as string).getTime() - new Date(b.ts as string).getTime()
        );
        const sensorSheet = rows.map((r) => {
          const raw = r.raw as Record<string, unknown> | null;
          return {
            "Date / Time (UTC)": r.ts ? new Date(r.ts as string).toUTCString() : "",
            "Temperature (°C)": r.temperature_c ?? "",
            "Humidity (%)": r.humidity_pct ?? "",
            "Light (lux)": r.light_lux ?? "",
            "Battery (%)": r.battery_pct ?? "",
            "Vibration (G)":
              raw != null ? ((raw["vibration_g"] ?? raw["vibration"] ?? "") as string) : "",
            "RSSI (dBm)": raw != null ? ((raw["rssi_dbm"] ?? raw["rssi"] ?? "") as string) : "",
            "GPS Coordinates": (raw?.latLng as string) ?? ""
          };
        });
        const ws1 = XLSX.utils.json_to_sheet(
          sensorSheet.length ? sensorSheet : [{ "(no data in range)": "" }]
        );
        ws1["!cols"] = [
          { wch: 28 },
          { wch: 16 },
          { wch: 14 },
          { wch: 13 },
          { wch: 12 },
          { wch: 14 },
          { wch: 12 },
          { wch: 24 }
        ];
        alignLeft(ws1);
        XLSX.utils.book_append_sheet(wb, ws1, "Sensor Data");
      }

      if (exportTrips.value && anyTripSelected.value) {
        const selectedTripIds = tripOptions.value.filter((t) => t.selected).map((t) => t.tripId);
        const coordRows: Record<string, unknown>[] = [];
        const tripResults = await Promise.all(
          selectedTripIds.map((tripId) =>
            (api.fleet.getTripById(tripId) as Promise<any>)
              .then((r) => ({ tripId, tripData: (r.details ?? r) as Record<string, unknown> }))
              .catch(() => ({ tripId, tripData: {} as Record<string, unknown> }))
          )
        );
        for (const { tripId, tripData } of tripResults) {
          let parsed: Record<string, unknown> = {};
          const raw = tripData?.trip_data;
          if (typeof raw === "string") {
            try {
              parsed = JSON.parse(raw);
            } catch {
              /* ignore */
            }
          } else if (raw && typeof raw === "object") parsed = raw as Record<string, unknown>;
          const points = (parsed["points"] as Record<string, unknown>[] | undefined) ?? [];
          points.forEach((p, idx) => {
            const ll = extractLatLng(p);
            if (!ll) return;
            coordRows.push({
              "Trip ID": tripId,
              "Point #": idx + 1,
              "Timestamp (UTC)": p.ts ? new Date(p.ts as string).toUTCString() : "",
              Latitude: ll.lat,
              Longitude: ll.lng,
              "Temperature (°C)": p.temp ?? p.temperature_c ?? ""
            });
          });
          if (selectedTripIds.length > 1) coordRows.push({} as Record<string, unknown>);
        }
        const ws2 = XLSX.utils.json_to_sheet(
          coordRows.length ? coordRows : [{ "(no trips selected)": "" }]
        );
        ws2["!cols"] = [
          { wch: 10 },
          { wch: 9 },
          { wch: 26 },
          { wch: 14 },
          { wch: 14 },
          { wch: 16 }
        ];
        alignLeft(ws2);
        XLSX.utils.book_append_sheet(wb, ws2, "Trip Coordinates");
      }

      if (exportShipment.value) {
        const settingsResult = await api.fleet.getDeviceSettings(hw);
        const settingsData = ((settingsResult as any).details ?? settingsResult) as Record<
          string,
          unknown
        >;
        const settingsRow = (settingsData?.row ?? settingsData) as Record<string, unknown>;
        let tripJson: Record<string, unknown> = {};
        let alarmJson: Record<string, unknown> = {};
        const rawTrip = settingsRow?.trip_json;
        const rawAlarm = settingsRow?.alarm_json;
        if (typeof rawTrip === "string") {
          try {
            tripJson = JSON.parse(rawTrip);
          } catch {
            /* ignore */
          }
        } else if (rawTrip && typeof rawTrip === "object")
          tripJson = rawTrip as Record<string, unknown>;
        if (typeof rawAlarm === "string") {
          try {
            alarmJson = JSON.parse(rawAlarm);
          } catch {
            /* ignore */
          }
        } else if (rawAlarm && typeof rawAlarm === "object")
          alarmJson = rawAlarm as Record<string, unknown>;

        const shipmentRows: { Field: string; Value: unknown }[] = [];
        shipmentRows.push({ Field: "── Shipment Info ──", Value: "" });
        for (const [label, key] of [
          ["Shipment ID", "shipment_id"],
          ["Goods", "goods"],
          ["Carrier", "carrier"],
          ["Sender", "sender"],
          ["Receiver", "receiver"],
          ["Customer Company", "customer_company"],
          ["Asset ID", "asset_id"],
          ["Vehicle", "vehicle"],
          ["Transport Route", "transport_route"],
          ["Start Location", "start_location"],
          ["End Location", "end_location"],
          ["Estimated Time", "estimated_time"],
          ["Remark", "remark"],
          ["Notify Email", "notify_email"]
        ] as [string, string][]) {
          shipmentRows.push({ Field: label, Value: tripJson[key] ?? "" });
        }
        shipmentRows.push({ Field: "", Value: "" });
        shipmentRows.push({ Field: "── Alarm Thresholds ──", Value: "" });
        for (const [label, key] of [
          ["Temp Min (°C)", "temp_min_c"],
          ["Temp Max (°C)", "temp_max_c"],
          ["Humidity Min (%)", "humidity_min_pct"],
          ["Humidity Max (%)", "humidity_max_pct"],
          ["Light Min (lux)", "light_min_lux"],
          ["Light Max (lux)", "light_max_lux"],
          ["Vibration Threshold (G)", "vibration_g"],
          ["Battery Min (%)", "battery_min_pct"]
        ] as [string, string][]) {
          shipmentRows.push({ Field: label, Value: alarmJson[key] ?? "" });
        }
        const ws3 = XLSX.utils.json_to_sheet(shipmentRows);
        ws3["!cols"] = [{ wch: 26 }, { wch: 44 }];
        alignLeft(ws3);
        XLSX.utils.book_append_sheet(wb, ws3, "Shipment & Settings");
      }

      if (exportAlarms.value) {
        const alarmStartIso = localInputToIso(alarmStart.value);
        const alarmEndIso = localInputToIso(alarmEnd.value);
        if (!alarmStartIso || !alarmEndIso)
          throw new Error("Please set a valid date range for Alarm Events.");
        if (new Date(alarmStartIso) >= new Date(alarmEndIso))
          throw new Error("Alarm Events start must be before end.");

        const alarmResult = await api.fleet.getAlarmLogRecent(hw, alarmStartIso, 5000);
        const alarmData = ((alarmResult as any).details ?? alarmResult) as {
          rows: Record<string, unknown>[];
        };
        const endMs = new Date(alarmEndIso).getTime();
        const logs = (alarmData?.rows ?? []).filter((l) => {
          const ts = l.ts ?? l.created_at;
          return ts ? new Date(ts as string).getTime() <= endMs : true;
        });
        const alarmRows = logs.map((l) => ({
          "Date / Time (UTC)": l.ts ? new Date(l.ts as string).toUTCString() : "",
          "Alarm Type": l.alarm_type ?? "",
          Field: l.field ?? "",
          Value: l.value ?? "",
          Threshold: l.threshold ?? "",
          Message: l.message ?? ""
        }));
        const ws4 = XLSX.utils.json_to_sheet(
          alarmRows.length ? alarmRows : [{ "(no alarms in range)": "" }]
        );
        ws4["!cols"] = [
          { wch: 28 },
          { wch: 16 },
          { wch: 18 },
          { wch: 12 },
          { wch: 12 },
          { wch: 40 }
        ];
        alignLeft(ws4);
        XLSX.utils.book_append_sheet(wb, ws4, "Alarm Events");
      }

      if (wb.SheetNames.length === 0)
        throw new Error("Nothing to export — enable at least one section.");
      const date = new Date().toISOString().slice(0, 10);
      XLSX.writeFile(wb, `Fleet_${hw}_${date}.xlsx`);
      step.value = "done";
    } catch (e: any) {
      error.value = e?.message ?? String(e);
    } finally {
      exporting.value = false;
    }
  }

  return {
    exportSensor,
    sensorStart,
    sensorEnd,
    exportTrips,
    tripOptions,
    tripsLoading,
    anyTripSelected,
    exportShipment,
    exportAlarms,
    alarmStart,
    alarmEnd,
    exporting,
    error,
    step,
    runExport
  };
}
