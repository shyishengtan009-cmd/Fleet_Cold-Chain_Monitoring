import { onUnmounted } from "vue";
import { useQuasar } from "quasar";
import * as signalR from "@microsoft/signalr";
import api from "@/helpers/api";
import { baseURL } from "@/helpers/api";
import { useNotificationStore } from "@/store/notification";
import { TOKEN_KEY } from "@/utils/constants";

interface AlarmLogEntry {
  id: number;
  hardware_id: string;
  ts: string;
  alarm_type: string;
  field: string;
  value: number | null;
  threshold: number | null;
  message: string;
  created_at: string;
}

interface AlarmLogResponse {
  hardwareId: string;
  count: number;
  rows: AlarmLogEntry[];
}

const POLL_MS = 15_000;
const MAX_UNREAD = 200;

export function useAlarmNotifier() {
  const $q = useQuasar();
  const notificationStore = useNotificationStore();

  let hubConnection: signalR.HubConnection | null = null;
  let fallbackTimer: ReturnType<typeof setInterval> | null = null;
  let currentHw: string | null = null;
  let currentTruckName = "";
  let lastSeenAt: string = new Date().toISOString();
  let pollInFlight = false;

  // ── shared notification renderer (used by both SignalR handler and poll fallback) ──

  function handleAlarmEntry(entry: AlarmLogEntry) {
    const label = currentTruckName || currentHw || entry.hardware_id;
    const isAlarm = entry.alarm_type === "ALARM";

    notificationStore.unread.unshift({
      notificationId: entry.id,
      senderName: isAlarm ? `Fleet Alarm — ${label}` : `Fleet Warning — ${label}`,
      userId: 0,
      message: `[${label}] ${entry.message}`,
      notificationCreationDate: entry.created_at,
      urlLink: "/monitoring/fleet/real-time"
    });
    if (notificationStore.unread.length > MAX_UNREAD) notificationStore.unread.splice(MAX_UNREAD);

    $q.notify({
      group: `fleet-alarm-${entry.hardware_id}`,
      type: isAlarm ? "negative" : "warning",
      icon: isAlarm ? "fa-solid fa-circle-exclamation" : "fa-solid fa-triangle-exclamation",
      message: `[${label}] ${entry.message}`,
      caption: isAlarm ? "Alarm triggered" : "Warning triggered",
      timeout: 0,
      position: "top-right",
      actions: [{ icon: "close", color: "white", round: true, handler: () => {} }]
    });
  }

  // ── poll (used as initial catch-up + fallback when SignalR cannot connect) ──

  async function poll() {
    if (!currentHw || pollInFlight) return;
    pollInFlight = true;
    try {
      const result = await api.fleet.getAlarmLogRecent(currentHw, lastSeenAt, 20);
      const data = ((result as any).details ?? result) as AlarmLogResponse;
      if (!data.rows?.length) return;

      lastSeenAt = data.rows[0].created_at;

      const label = currentTruckName || currentHw;

      // ONE combined toast when multiple alarms arrive in the same poll window
      const latest = data.rows[0];
      const isAlarm = latest.alarm_type === "ALARM";
      const multiCaption =
        data.rows.length > 1
          ? `+${data.rows.length - 1} more alarm${data.rows.length > 2 ? "s" : ""} — latest above`
          : isAlarm
            ? "Alarm triggered"
            : "Warning triggered";

      for (const entry of data.rows) {
        notificationStore.unread.unshift({
          notificationId: -entry.id,
          senderName:
            entry.alarm_type === "ALARM" ? `Fleet Alarm — ${label}` : `Fleet Warning — ${label}`,
          userId: 0,
          message: `[${label}] ${entry.message}`,
          notificationCreationDate: entry.created_at,
          urlLink: "/monitoring/fleet/real-time"
        });
      }
      if (notificationStore.unread.length > MAX_UNREAD) notificationStore.unread.splice(MAX_UNREAD);

      $q.notify({
        group: `fleet-alarm-${currentHw}`,
        type: isAlarm ? "negative" : "warning",
        icon: isAlarm ? "fa-solid fa-circle-exclamation" : "fa-solid fa-triangle-exclamation",
        message: `[${label}] ${latest.message}`,
        caption: multiCaption,
        timeout: 0,
        position: "top-right",
        actions: [{ icon: "close", color: "white", round: true, handler: () => {} }]
      });
    } catch {
      // best-effort — silent fail
    } finally {
      pollInFlight = false;
    }
  }

  // ── SignalR connect ────────────────────────────────────────────────────────

  async function connectHub(hw: string) {
    hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${baseURL}/fleet-alarm-hub`, {
        // WebSocket connections cannot send custom headers; pass JWT as query param.
        // Backend reads it via JwtBearerEvents.OnMessageReceived in Program.cs.
        accessTokenFactory: () => localStorage.getItem(TOKEN_KEY) ?? "",
      })
      .withAutomaticReconnect()
      .build();

    hubConnection.on("ReceiveAlarm", (entry: AlarmLogEntry) => {
      if (entry.hardware_id !== currentHw) return;
      handleAlarmEntry(entry);
    });

    // Re-subscribe to the device group after automatic reconnect.
    // withAutomaticReconnect() creates a new connection ID — the server's group
    // membership is tied to the old ID and must be re-registered.
    hubConnection.onreconnected(async () => {
      if (currentHw) {
        try {
          await hubConnection!.invoke("Subscribe", currentHw);
        } catch (_) {
          /* reconnect subscribe failed — hub will retry on next reconnect */
        }
      }
    });

    await hubConnection.start();
    await hubConnection.invoke("Subscribe", hw);
  }

  // ── Public API ─────────────────────────────────────────────────────────────

  async function start(hw: string, truckName?: string | null) {
    await stop();
    currentHw = hw;
    currentTruckName = truckName || hw;
    lastSeenAt = new Date().toISOString();

    // Initial catch-up fetch (picks up alarms that fired before we connected)
    await poll();

    // Try SignalR; fall back to polling if unavailable
    try {
      await connectHub(hw);
    } catch {
      fallbackTimer = setInterval(poll, POLL_MS);
    }
  }

  async function stop() {
    if (fallbackTimer) {
      clearInterval(fallbackTimer);
      fallbackTimer = null;
    }
    if (hubConnection) {
      try {
        if (hubConnection.state === signalR.HubConnectionState.Connected && currentHw)
          await hubConnection.invoke("Unsubscribe", currentHw);
        await hubConnection.stop();
      } catch {
        // ignore cleanup errors
      }
      hubConnection = null;
    }
    currentHw = null;
  }

  function setDevice(hw: string, truckName?: string | null) {
    // setDevice is called when the user switches devices — fire-and-forget reconnect
    start(hw, truckName).catch(() => {});
  }

  async function simulate() {
    // If a real device is active, call the backend — exercises the full pipeline
    // (DB insert + SignalR push). The SignalR push fires back to this client via
    // ReceiveAlarm, so the toast appears through the normal handler.
    if (currentHw) {
      try {
        await api.fleet.testAlarm(currentHw);
        return;
      } catch {
        // fall through to local-only toast
      }
    }

    // No device connected or backend unreachable — show a local toast only
    const label = currentTruckName || (currentHw ?? "TEST-DEVICE");
    const fakeEntry: AlarmLogEntry = {
      id: -Date.now(),
      hardware_id: currentHw ?? "TEST-DEVICE",
      ts: new Date().toISOString(),
      alarm_type: "ALARM",
      field: "temperature",
      value: 35.2,
      threshold: 30,
      message: `[TEST] Temperature 35.2°C exceeds threshold 30°C on device ${currentHw ?? "TEST-DEVICE"}`,
      created_at: new Date().toISOString()
    };

    notificationStore.unread.unshift({
      notificationId: fakeEntry.id,
      senderName: `Fleet Alarm — ${label}`,
      userId: 0,
      message: fakeEntry.message,
      notificationCreationDate: fakeEntry.created_at,
      urlLink: "/monitoring/fleet/real-time"
    });

    $q.notify({
      group: `fleet-alarm-${currentHw ?? "TEST-DEVICE"}`,
      type: "negative",
      icon: "fa-solid fa-circle-exclamation",
      message: fakeEntry.message,
      caption: "Alarm triggered",
      timeout: 0,
      position: "top-right",
      actions: [{ icon: "close", color: "white", round: true, handler: () => {} }]
    });
  }

  onUnmounted(() => {
    stop().catch(() => {});
  });

  return { start, stop, setDevice, simulate, checkNow: poll };
}
