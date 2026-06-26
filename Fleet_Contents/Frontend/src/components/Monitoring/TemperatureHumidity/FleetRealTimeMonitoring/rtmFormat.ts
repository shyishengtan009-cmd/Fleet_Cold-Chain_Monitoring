export function fmt(v: number | null | undefined, decimals = 1, suffix = ""): string {
  return v != null ? `${Number(v).toFixed(decimals)}${suffix}` : "—";
}

export function ageMin(s: number | null): string {
  return s != null ? String(Math.round(s / 60)) : "—";
}

export function fmtTs(iso: string | null): string {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleString("en-GB", {
      timeZone: "Asia/Kuala_Lumpur",
      day: "2-digit",
      month: "short",
      year: "numeric",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
      hour12: false
    });
  } catch {
    return iso;
  }
}
