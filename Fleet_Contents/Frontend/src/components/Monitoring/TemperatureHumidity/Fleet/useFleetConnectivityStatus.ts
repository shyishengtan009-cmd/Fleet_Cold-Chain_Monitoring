// Single source of truth for "is this device online" badge styling across Fleet.
// Device connectivity status only — alarm severity (ALARM/WARNING/OPTIMAL) is a
// separate concept and intentionally not handled here.

export type ConnectivityStatus = "OK" | "WARN" | "OFFLINE";

export interface ConnectivityBadge {
  color: string;
  textColor: string;
  label: string;
  icon: string;
}

const BADGES: Record<ConnectivityStatus, ConnectivityBadge> = {
  OK: { color: "green-2", textColor: "black", label: "ACTIVE", icon: "fa-solid fa-circle-check" },
  WARN: {
    color: "orange-2",
    textColor: "black",
    label: "WARNING",
    icon: "fa-solid fa-triangle-exclamation"
  },
  OFFLINE: {
    color: "red-2",
    textColor: "black",
    label: "OFFLINE",
    icon: "fa-solid fa-circle-xmark"
  }
};

const AWAITING_DATA: ConnectivityBadge = {
  color: "grey-2",
  textColor: "black",
  label: "AWAITING DATA",
  icon: "fa-solid fa-hourglass-half"
};

// status === null/undefined means "no reading yet" — distinct from OFFLINE
// (OFFLINE means it WAS reporting and stopped; AWAITING DATA means it never has).
export function connectivityBadge(status: ConnectivityStatus | null | undefined): ConnectivityBadge {
  if (!status) return AWAITING_DATA;
  return BADGES[status] ?? AWAITING_DATA;
}

// For pages that only have a raw timestamp (no server-computed status field) —
// same WARN/OFFLINE thresholds as the backend (FleetDashboard.vue: WARN_SEC = 15*60,
// OFFLINE_SEC = 30*60) so client-computed and server-computed status never disagree.
export function connectivityStatusFromAge(
  ts: string | null,
  warnSec = 900,
  offlineSec = 1800
): ConnectivityStatus | null {
  if (!ts) return null;
  const ageSeconds = (Date.now() - new Date(ts).getTime()) / 1000;
  if (ageSeconds >= offlineSec) return "OFFLINE";
  if (ageSeconds >= warnSec) return "WARN";
  return "OK";
}

export function connectivityBadgeFromAge(
  ts: string | null,
  warnSec = 900,
  offlineSec = 1800
): ConnectivityBadge {
  return connectivityBadge(connectivityStatusFromAge(ts, warnSec, offlineSec));
}
