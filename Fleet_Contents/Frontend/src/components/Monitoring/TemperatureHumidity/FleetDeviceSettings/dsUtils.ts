export function asObject(v: unknown): Record<string, unknown> {
  if (!v) return {};
  if (typeof v === "string") {
    try {
      const p = JSON.parse(v);
      return p && typeof p === "object" && !Array.isArray(p) ? (p as Record<string, unknown>) : {};
    } catch {
      return {};
    }
  }
  if (typeof v === "object" && !Array.isArray(v)) return v as Record<string, unknown>;
  return {};
}

export function pickScalar(v: unknown): unknown {
  if (Array.isArray(v)) return v.length ? v[0] : null;
  return v ?? null;
}

export function toNum(v: unknown): number | null {
  const x = pickScalar(v);
  if (x === null || x === "" || x === undefined) return null;
  const n = Number(x);
  return Number.isFinite(n) ? n : null;
}

export function toStr(v: unknown): string {
  const x = pickScalar(v);
  return x === null || x === undefined ? "" : String(x);
}

export function toBool(v: unknown): boolean {
  const x = pickScalar(v);
  if (x === null || x === undefined) return false;
  if (typeof x === "boolean") return x;
  if (typeof x === "string") return x === "true" || x === "1";
  return Boolean(x);
}
