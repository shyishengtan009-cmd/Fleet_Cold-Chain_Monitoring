<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from "vue";
import { tripProgressState } from "./useTripProgressState";

const props = defineProps<{ trip: any }>();

// ── Display helpers ───────────────────────────────────────────────────────────
function val(v: unknown): string {
  const s = v == null ? "" : String(v).trim();
  return s || "—";
}

const startLabel = computed(() => val(props.trip?.start_location ?? props.trip?.startLocation));
const endLabel = computed(() => val(props.trip?.end_location ?? props.trip?.endLocation));
const estimatedLabel = computed(() => val(props.trip?.estimated_time ?? props.trip?.estimatedTime));

const hasRouteData = computed(
  () =>
    (props.trip?.start_location ?? props.trip?.startLocation) ||
    (props.trip?.end_location ?? props.trip?.endLocation)
);

// ── Progress timer ────────────────────────────────────────────────────────────
function parseMaxMs(): number | null {
  const raw = props.trip?.estimated_time ?? props.trip?.estimatedTime;
  if (raw != null) {
    const s = String(raw).trim();
    if (s.includes(":")) {
      const [hh, mm] = s.split(":").map(Number);
      if (
        Number.isFinite(hh) &&
        Number.isFinite(mm) &&
        hh >= 0 &&
        hh <= 23 &&
        mm >= 0 &&
        mm <= 59
      ) {
        const totalS = hh * 3600 + mm * 60;
        if (totalS > 0) return totalS * 1000;
      }
    }
    const n = Number(s);
    if (Number.isFinite(n) && n > 0) return n * 60 * 1000;
  }
  const maxMin = props.trip?.trip_max_minutes ?? props.trip?.tripMaxMinutes;
  if (maxMin != null) {
    const n = Number(maxMin);
    if (Number.isFinite(n) && n > 0) return n * 60 * 1000;
  }
  return null;
}

const progress = ref(0);
const elapsedMs = ref(0);
// Timestamp when this component mounted — used as fallback start when no DB trip is open
const mountedAt = ref<string | null>(null);
// Set when the user saves a new estimated_time — overrides active trip start so bar resets to 0
const estimateResetAt = ref<string | null>(null);
let timer: ReturnType<typeof setInterval> | null = null;
let resetTimeout: ReturnType<typeof setTimeout> | null = null;

function clearResetTimeout() {
  if (resetTimeout) clearTimeout(resetTimeout);
  resetTimeout = null;
}

function scheduleAutoReset() {
  clearResetTimeout();
  resetTimeout = setTimeout(
    () => {
      estimateResetAt.value = new Date().toISOString();
      mountedAt.value = estimateResetAt.value;
      startTimer();
    },
    5 * 60 * 1000
  );
}

function stopTimer() {
  if (timer) clearInterval(timer);
  timer = null;
}

function startTimer() {
  stopTimer();
  clearResetTimeout();

  // Priority 0: user just saved a new estimated_time — reset bar to 0 from NOW
  // regardless of whether a DB trip is active (so the bar reflects the new duration)
  let isoStart: string | null = estimateResetAt.value;

  // Priority 1: use the actual trip start from shared state (Map tab / scheduler open trip)
  if (!isoStart) isoStart = tripProgressState.value.startTimeIso;

  // Priority 2: when no DB trip is active but route + duration is configured,
  // fall back to mount time so the bar moves as soon as the user sees it.
  if (!isoStart && hasRouteData.value && parseMaxMs() !== null) {
    isoStart = mountedAt.value;
  }

  const startMs = isoStart ? new Date(isoStart).getTime() : null;
  if (!startMs) {
    progress.value = 0;
    elapsedMs.value = 0;
    return;
  }

  timer = setInterval(() => {
    const now = Date.now();
    const durMs = parseMaxMs();
    elapsedMs.value = now - startMs;
    if (!durMs || durMs <= 0) {
      progress.value = 0;
      return;
    }
    progress.value = Math.min(elapsedMs.value / durMs, 1);
    if (progress.value >= 1) {
      stopTimer();
      scheduleAutoReset();
    }
  }, 200);
}

onMounted(() => {
  mountedAt.value = new Date().toISOString();
  startTimer();
});
onUnmounted(() => {
  stopTimer();
  clearResetTimeout();
});

// Restart when a real trip starts or stops
watch(
  () => tripProgressState.value.startTimeIso,
  (newVal) => {
    if (!newVal) mountedAt.value = new Date().toISOString();
    // A newly opened DB trip supersedes the manual estimate-reset anchor
    if (newVal) estimateResetAt.value = null;
    startTimer();
  }
);

// Only reset when estimated_time / trip_max_minutes actually change value.
// fetchSettings is called periodically — if the user hasn't touched Device Settings
// the values are identical, so the bar should keep its current position.
watch(
  () =>
    [
      props.trip?.estimated_time ?? props.trip?.estimatedTime,
      props.trip?.trip_max_minutes ?? props.trip?.tripMaxMinutes
    ] as const,
  ([newEst, newMax], [oldEst, oldMax]) => {
    if (newEst === oldEst && newMax === oldMax) return;
    // User saved a new duration — restart bar from NOW even if a trip is active
    estimateResetAt.value = new Date().toISOString();
    mountedAt.value = estimateResetAt.value;
    startTimer();
  }
);

// ── Derived display ───────────────────────────────────────────────────────────
// Clamp truck icon away from edges so it's always visible
const truckLeft = computed(() => `${Math.min(Math.max(progress.value * 100, 3), 97)}%`);

// Bar colour: green → orange at 75% → red when overdue
const barColor = computed(() => {
  if (progress.value >= 1) return "negative";
  if (progress.value >= 0.75) return "warning";
  return "positive";
});

const truckColor = computed(() => {
  if (progress.value >= 1) return "negative";
  if (progress.value >= 0.75) return "warning";
  return "primary";
});

function fmtMs(ms: number): string {
  const h = Math.floor(ms / 3_600_000);
  const m = Math.floor((ms % 3_600_000) / 60_000);
  return h > 0 ? `${h}h ${m}m` : `${m}m`;
}

const elapsedLabel = computed(() => {
  if (elapsedMs.value <= 0) return null;
  return fmtMs(elapsedMs.value);
});

const remainingLabel = computed(() => {
  const durMs = parseMaxMs();
  if (!durMs || elapsedMs.value <= 0) return null;
  const rem = durMs - elapsedMs.value;
  if (rem <= 0) return "Overdue";
  return `${fmtMs(rem)} left`;
});

const pctLabel = computed(() => `${Math.round(progress.value * 100)}%`);
</script>

<template>
  <div v-if="hasRouteData" class="trip-indicator q-px-md q-py-sm">
    <!-- Header row -->
    <div class="row items-center q-mb-xs">
      <q-icon name="fa-solid fa-route" size="11px" color="grey-6" class="q-mr-xs" />
      <span class="text-caption text-grey-6 text-weight-medium" style="font-size: 11px">
        Trip Route
      </span>
      <q-space />
      <!-- Elapsed / remaining / pct -->
      <div style="display: flex; align-items: center; gap: 8px">
        <span v-if="elapsedLabel" class="text-caption elapsed-label" style="font-size: 10px">
          {{ elapsedLabel }} elapsed
        </span>
        <span
          v-if="remainingLabel"
          class="text-caption text-weight-medium"
          :style="{
            fontSize: '10px',
            color: progress >= 1 ? '#d32f2f' : progress >= 0.75 ? '#f57c00' : '#2e7d32'
          }"
        >
          {{ remainingLabel }}
        </span>
        <span
          v-if="estimatedLabel !== '—'"
          class="text-caption text-grey-5"
          style="font-size: 10px"
        >
          Est. {{ estimatedLabel }}
        </span>
      </div>
    </div>

    <!-- Route bar -->
    <div class="row no-wrap items-center" style="gap: 8px">
      <!-- Origin -->
      <div class="route-location">
        <q-icon name="fa-solid fa-circle-dot" size="9px" color="primary" class="q-mr-xs" />
        <span class="text-caption text-weight-medium text-primary ellipsis">{{ startLabel }}</span>
      </div>

      <!-- Progress track -->
      <div
        class="col"
        style="
          position: relative;
          min-width: 60px;
          height: 22px;
          display: flex;
          align-items: center;
        "
      >
        <q-linear-progress
          :value="progress"
          :color="barColor"
          track-color="grey-3"
          size="7px"
          rounded
          style="width: 100%"
        />
        <q-icon
          name="fa-solid fa-truck"
          size="14px"
          :color="truckColor"
          class="truck-icon"
          :style="{ left: truckLeft }"
        />
        <!-- % label floats above the midpoint -->
        <span
          v-if="progress > 0"
          class="pct-label"
          :style="{
            left: truckLeft,
            color: progress >= 1 ? '#d32f2f' : progress >= 0.75 ? '#f57c00' : '#2e7d32'
          }"
        >
          {{ pctLabel }}
        </span>
      </div>

      <!-- Destination -->
      <div class="route-location route-location--end">
        <span class="text-caption text-weight-medium text-grey-7 ellipsis">{{ endLabel }}</span>
        <q-icon name="fa-solid fa-flag-checkered" size="9px" color="grey-6" class="q-ml-xs" />
      </div>
    </div>
  </div>
</template>

<style lang="sass" scoped>
@import '../../../../style/_variables'

.trip-indicator
  background: $primary-grey-1
  border-top: 1px solid $secondary-grey-2
  border-bottom: 1px solid $secondary-grey-2

.elapsed-label
  color: $secondary-grey-1

.route-location
  display: flex
  align-items: center
  max-width: 130px
  flex-shrink: 0

.route-location--end
  justify-content: flex-end

.truck-icon
  position: absolute
  top: 50%
  transform: translate(-50%, -50%)
  pointer-events: none
  transition: left 0.2s linear

.pct-label
  position: absolute
  top: -1px
  transform: translateX(-50%)
  font-size: 9px
  font-weight: 700
  pointer-events: none
  white-space: nowrap
  line-height: 1
</style>
