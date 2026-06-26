<script lang="ts">
import { asObject, toBool, toNum, toStr } from "./dsUtils";

export type AlarmSettings = {
  pre_alarm: boolean;
  temp_min_c: number | null;
  temp_max_c: number | null;
  humidity_min_pct: number | null;
  humidity_max_pct: number | null;
  light_min_lux: number | null;
  light_max_lux: number | null;
  vibration_g: number | null;
  effective_start: string;
  effective_end: string;
  daily_start: string;
  daily_end: string;
  repeat_days: string[];
  auto_trip: boolean;
  notify_email: string;
  email_enabled: boolean;
  debounce_count: number | null;
  email_cooldown_minutes: number | null;
  dwell_enabled: boolean;
  dwell_max_minutes: number | null;
  reporting_interval_minutes: number | null;
  door_open_lux_threshold: number | null;
};

export const alarmDefaults: AlarmSettings = {
  pre_alarm: false,
  temp_min_c: null,
  temp_max_c: null,
  humidity_min_pct: null,
  humidity_max_pct: null,
  light_min_lux: null,
  light_max_lux: null,
  vibration_g: null,
  effective_start: "",
  effective_end: "",
  daily_start: "",
  daily_end: "",
  repeat_days: [],
  auto_trip: false,
  notify_email: "",
  email_enabled: false,
  debounce_count: 3,
  email_cooldown_minutes: 30,
  dwell_enabled: false,
  dwell_max_minutes: 15,
  reporting_interval_minutes: null,
  door_open_lux_threshold: null
};

export function normalizeAlarm(obj: unknown): AlarmSettings {
  const a = asObject(obj);
  return {
    ...alarmDefaults,
    pre_alarm: toBool(a.pre_alarm),
    temp_min_c: toNum(a.temp_min_c),
    temp_max_c: toNum(a.temp_max_c),
    humidity_min_pct: toNum(a.humidity_min_pct),
    humidity_max_pct: toNum(a.humidity_max_pct),
    light_min_lux: toNum(a.light_min_lux),
    light_max_lux: toNum(a.light_max_lux),
    vibration_g: toNum(a.vibration_g),
    effective_start: toStr(a.effective_start),
    effective_end: toStr(a.effective_end),
    daily_start: toStr(a.daily_start),
    daily_end: toStr(a.daily_end),
    repeat_days: Array.isArray(a.repeat_days) ? a.repeat_days.map(String) : [],
    auto_trip: toBool(a.auto_trip),
    notify_email: toStr(a.notify_email),
    email_enabled: toBool(a.email_enabled),
    debounce_count: toNum(a.debounce_count) ?? 3,
    email_cooldown_minutes: toNum(a.email_cooldown_minutes) ?? 30,
    dwell_enabled: toBool(a.dwell_enabled),
    dwell_max_minutes: toNum(a.dwell_max_minutes) ?? 15,
    reporting_interval_minutes: toNum(a.reporting_interval_minutes),
    door_open_lux_threshold: toNum(a.door_open_lux_threshold)
  };
}

export function serializeAlarm(a: AlarmSettings) {
  return {
    pre_alarm: Boolean(a.pre_alarm),
    temp_min_c: a.temp_min_c == null ? null : Number(a.temp_min_c),
    temp_max_c: a.temp_max_c == null ? null : Number(a.temp_max_c),
    humidity_min_pct: a.humidity_min_pct == null ? null : Number(a.humidity_min_pct),
    humidity_max_pct: a.humidity_max_pct == null ? null : Number(a.humidity_max_pct),
    light_min_lux: a.light_min_lux == null ? null : Number(a.light_min_lux),
    light_max_lux: a.light_max_lux == null ? null : Number(a.light_max_lux),
    vibration_g: a.vibration_g == null ? null : Number(a.vibration_g),
    effective_start: String(a.effective_start || ""),
    effective_end: String(a.effective_end || ""),
    daily_start: String(a.daily_start || ""),
    daily_end: String(a.daily_end || ""),
    repeat_days: Array.isArray(a.repeat_days) ? a.repeat_days.map(String) : [],
    auto_trip: Boolean(a.auto_trip),
    notify_email: String(a.notify_email || ""),
    email_enabled: Boolean(a.email_enabled),
    debounce_count: a.debounce_count == null ? 3 : Number(a.debounce_count),
    email_cooldown_minutes:
      a.email_cooldown_minutes == null ? 30 : Number(a.email_cooldown_minutes),
    dwell_enabled: Boolean(a.dwell_enabled),
    dwell_max_minutes: a.dwell_max_minutes == null ? 15 : Number(a.dwell_max_minutes),
    reporting_interval_minutes: a.reporting_interval_minutes == null ? null : Number(a.reporting_interval_minutes),
    door_open_lux_threshold: a.door_open_lux_threshold == null ? null : Number(a.door_open_lux_threshold)
  };
}
</script>

<script setup lang="ts">
import { computed } from "vue";

import InputDateApp from "@/components/common/InputDateApp.vue";
import LabelApp from "@/components/common/LabelApp.vue";

const props = defineProps<{ modelValue: AlarmSettings; disable?: boolean }>();
const emit = defineEmits<{
  (e: "update:modelValue", v: AlarmSettings): void;
  (e: "validation-error", hasError: boolean): void;
}>();

const model = computed<AlarmSettings>({
  get: () => props.modelValue,
  set: (v) => emit("update:modelValue", v)
});

// ── Validation ──────────────────────────────────────────────────────────────

const errors = computed(() => {
  const a = model.value;
  const e: Record<string, string> = {};

  if (a.temp_min_c != null && a.temp_max_c != null && a.temp_min_c >= a.temp_max_c)
    e.temp = "Temp Min must be less than Temp Max.";

  if (a.humidity_min_pct != null && a.humidity_max_pct != null && a.humidity_min_pct >= a.humidity_max_pct)
    e.humidity = "Humidity Min must be less than Humidity Max.";

  if (a.light_min_lux != null && a.light_max_lux != null && a.light_min_lux >= a.light_max_lux)
    e.light = "Light Min must be less than Light Max.";

  if (a.debounce_count != null && a.debounce_count < 1)
    e.debounce = "Debounce count must be at least 1.";

  if (a.email_cooldown_minutes != null && a.email_cooldown_minutes < 0)
    e.cooldown = "Email cooldown must be 0 or greater.";

  if (a.reporting_interval_minutes != null && (a.reporting_interval_minutes < 1 || a.reporting_interval_minutes > 60))
    e.interval = "Reporting interval must be 1–60 minutes.";

  if (a.dwell_max_minutes != null && a.dwell_max_minutes < 1)
    e.dwell = "Max parked time must be at least 1 minute.";

  if (a.daily_start && a.daily_end && a.daily_start >= a.daily_end)
    e.schedule = "End time must be after start time — overnight schedules (e.g. 22:00 → 06:00) are not supported.";

  return e;
});

const hasErrors = computed(() => Object.keys(errors.value).length > 0);

defineExpose({ hasErrors });

const dayOptions = [
  { label: "Sun", value: "Sun" },
  { label: "Mon", value: "Mon" },
  { label: "Tue", value: "Tue" },
  { label: "Wed", value: "Wed" },
  { label: "Thu", value: "Thu" },
  { label: "Fri", value: "Fri" },
  { label: "Sat", value: "Sat" }
];

function update<K extends keyof AlarmSettings>(key: K, value: AlarmSettings[K]) {
  model.value = { ...model.value, [key]: value };
}

function parseNumber(v: unknown): number | null {
  if (v === "" || v === null || v === undefined) return null;
  const n = Number(v);
  return Number.isFinite(n) ? n : null;
}

function isoToDisplay(iso: string): string {
  if (!iso) return "";
  const raw = iso.includes("T") ? iso.split("T")[0] : iso;
  const parts = raw.split("-");
  if (parts.length === 3) return `${parts[2]}/${parts[1]}/${parts[0]}`;
  return "";
}

function displayToIso(ddmmyyyy: string): string {
  if (!ddmmyyyy) return "";
  const parts = ddmmyyyy.split("/");
  if (parts.length !== 3) return "";
  return `${parts[2]}-${parts[1].padStart(2, "0")}-${parts[0].padStart(2, "0")}`;
}
</script>

<template>
  <div class="q-pa-md">
    <!-- Sensor Thresholds -->
    <div class="row q-col-gutter-sm items-end q-mb-md">
      <LabelApp label="Temp Min (°C)" class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          type="number"
          :disable="props.disable"
          :model-value="model.temp_min_c"
          @update:model-value="(v) => update('temp_min_c', parseNumber(v))"
        />
      </LabelApp>
      <LabelApp label="Temp Max (°C)" class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          type="number"
          :disable="props.disable"
          :model-value="model.temp_max_c"
          @update:model-value="(v) => update('temp_max_c', parseNumber(v))"
        />
      </LabelApp>
      <LabelApp label="Humidity Min (%)" class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          type="number"
          :disable="props.disable"
          :model-value="model.humidity_min_pct"
          @update:model-value="(v) => update('humidity_min_pct', parseNumber(v))"
        />
      </LabelApp>
      <LabelApp label="Humidity Max (%)" class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          type="number"
          :disable="props.disable"
          :model-value="model.humidity_max_pct"
          @update:model-value="(v) => update('humidity_max_pct', parseNumber(v))"
        />
      </LabelApp>
      <LabelApp label="Light Min (lux)" class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          type="number"
          :disable="props.disable"
          :model-value="model.light_min_lux"
          @update:model-value="(v) => update('light_min_lux', parseNumber(v))"
        />
      </LabelApp>
      <LabelApp label="Light Max (lux)" class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          type="number"
          :disable="props.disable"
          :model-value="model.light_max_lux"
          @update:model-value="(v) => update('light_max_lux', parseNumber(v))"
        />
      </LabelApp>
      <LabelApp label="Vibration Threshold (g)" class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          type="number"
          step="0.1"
          :disable="props.disable"
          :model-value="model.vibration_g"
          @update:model-value="(v) => update('vibration_g', parseNumber(v))"
        />
      </LabelApp>
      <LabelApp label="Reporting Interval (min)" tooltip="How often the device sends data to the cloud" class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          type="number"
          min="1"
          max="60"
          :disable="props.disable"
          :model-value="model.reporting_interval_minutes"
          :error="!!errors.interval"
          :error-message="errors.interval"
          @update:model-value="(v) => update('reporting_interval_minutes', parseNumber(v))"
        />
      </LabelApp>
      <div class="col col-12 col-sm-4 col-md-4 flex items-end" style="padding-bottom: 2px">
        <q-toggle
          dense
          label="Enable Pre-Alarm"
          :disable="props.disable"
          :model-value="model.pre_alarm"
          @update:model-value="(v) => update('pre_alarm', !!v)"
        />
      </div>
    </div>

    <!-- Threshold validation errors -->
    <div v-if="!props.disable && hasErrors" class="q-mb-md">
      <q-banner
        v-for="(msg, key) in errors"
        :key="key"
        dense
        rounded
        class="bg-red-1 text-red-9 q-mb-xs"
        style="font-size:13px"
      >
        <template #avatar><q-icon name="fa-solid fa-circle-xmark" color="negative" size="14px" /></template>
        {{ msg }}
      </q-banner>
    </div>

    <q-separator class="q-mb-md" />

    <!-- Active Schedule -->
    <div class="row q-col-gutter-sm items-end q-mb-md">
      <div class="col col-12 col-sm-4 col-md-4" :style="props.disable ? 'pointer-events:none;opacity:0.38' : ''">
        <InputDateApp
          label="Effective Start"
          :date="isoToDisplay(model.effective_start)"
          @on-change="(v: string) => update('effective_start', displayToIso(v))"
        />
      </div>
      <div class="col col-12 col-sm-4 col-md-4" :style="props.disable ? 'pointer-events:none;opacity:0.38' : ''">
        <InputDateApp
          label="Effective End"
          :date="isoToDisplay(model.effective_end)"
          @on-change="(v: string) => update('effective_end', displayToIso(v))"
        />
      </div>
      <LabelApp label="Daily Start" tooltip="Alarm is only active between these times. Overnight ranges (e.g. 22:00 → 06:00) are not supported — end time must be later than start time." class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          type="time"
          :disable="props.disable"
          :model-value="model.daily_start"
          :error="!!errors.schedule"
          :error-message="errors.schedule"
          @update:model-value="(v) => update('daily_start', String(v || ''))"
        />
      </LabelApp>
      <LabelApp label="Daily End" tooltip="Alarm is only active between these times. Overnight ranges (e.g. 22:00 → 06:00) are not supported — end time must be later than start time." class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          type="time"
          :disable="props.disable"
          :model-value="model.daily_end"
          :error="!!errors.schedule"
          :error-message="errors.schedule"
          @update:model-value="(v) => update('daily_end', String(v || ''))"
        />
      </LabelApp>
    </div>
    <div class="q-mb-md">
      <LabelApp label="Repeat Days">
        <q-btn-toggle
          dense
          unelevated
          color="primary"
          toggle-color="primary"
          no-caps
          multiple
          :disable="props.disable"
          :options="dayOptions"
          :model-value="model.repeat_days"
          @update:model-value="(v) => update('repeat_days', Array.isArray(v) ? v : [])"
        />
      </LabelApp>
    </div>
    <div class="q-mb-md">
      <q-toggle
        dense
        label="Enable Auto Trip (scheduler will start/stop trips based on the schedule above)"
        :disable="props.disable"
        :model-value="model.auto_trip"
        @update:model-value="(v) => update('auto_trip', !!v)"
      />
    </div>

    <q-separator class="q-mb-md" />

    <!-- Dwell / Prolonged-Stop Alert -->
    <div
      class="text-caption text-weight-bold text-grey-7 q-mb-sm"
      style="text-transform: uppercase; font-size: 11px"
    >
      Dwell Alert (Driver Stationary Too Long)
    </div>
    <div class="row q-col-gutter-sm items-center q-mb-sm">
      <div class="col col-12 col-sm-4 col-md-4 flex items-center">
        <q-toggle
          dense
          label="Enable Dwell Alert"
          :disable="props.disable"
          :model-value="model.dwell_enabled"
          @update:model-value="(v) => update('dwell_enabled', !!v)"
        />
      </div>
      <LabelApp label="Max Parked Time (min)" tooltip="Alert fires when truck stays within 100 m of the same spot for longer than this limit. Named locations (set on the map) can override this per-stop." class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          type="number"
          min="1"
          :disable="props.disable || !model.dwell_enabled"
          :model-value="model.dwell_max_minutes"
          @update:model-value="(v) => update('dwell_max_minutes', parseNumber(v) ?? 15)"
        />
      </LabelApp>
    </div>

    <q-separator class="q-mb-md" />

    <!-- Cargo Door Open Detection -->
    <div class="text-caption text-weight-bold text-grey-7 q-mb-sm" style="text-transform: uppercase; font-size: 11px">
      Cargo Door Open Detection
    </div>
    <div class="row q-col-gutter-sm items-center q-mb-sm">
      <LabelApp label="Door Open Lux Threshold" tooltip="Fires when light exceeds this value during an active trip. Typical cold cargo: &lt;50 lux (dark); door open: 300–2000 lux. Leave blank to disable." class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          type="number"
          min="0"
          placeholder="e.g. 500 lux"
          :disable="props.disable"
          :model-value="model.door_open_lux_threshold"
          @update:model-value="(v) => update('door_open_lux_threshold', parseNumber(v))"
        />
      </LabelApp>
    </div>
  </div>
</template>
