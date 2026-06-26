<!-- src/components/Monitoring/TemperatureHumidity/FleetDeviceSettings/DeviceSettings.vue -->
<script setup lang="ts">
import { computed, onMounted, ref, toRaw } from "vue";
import { onBeforeRouteLeave } from "vue-router";

import api from "@/helpers/api";
import { useRowHighlight } from "@/helpers/rowHighlight";

import DeviceEditModal from "./DeviceEditModal.vue";
import DsAlarm, {
  type AlarmSettings,
  alarmDefaults,
  normalizeAlarm,
  serializeAlarm
} from "./DsAlarm.vue";
import DsDeviceSelect from "./DsDeviceSelect.vue";
import DsHeader from "./DsHeader.vue";
import DsNotification, { type NotificationSettings } from "./DsNotification.vue";
import DsTrip, {
  type TripSettings,
  tripDefaults,
  normalizeTrip,
  serializeTrip
} from "./DsTrip.vue";
import DsLocations from "./DsLocations.vue";
import FleetDevicePortal from "../FleetRealTimeMonitoring/FleetDevicePortal.vue";
import { connectivityBadgeFromAge } from "../Fleet/useFleetConnectivityStatus";

// ── Types ────────────────────────────────────────────────────────────────────

interface DeviceItem {
  hardware_id: string;
  label: string | null;
  device_int_id: number | null;
  has_polling: boolean;
  has_custom_creds: boolean;
  registered_at: string | null;
  // telemetry (null = awaiting first data)
  ts: string | null;
  temperature_c: number | null;
  humidity_pct: number | null;
  battery_pct: number | null;
  truck_name: string | null;
}

// ── State ────────────────────────────────────────────────────────────────────

const deviceItems = ref<DeviceItem[]>([]);
const selectedHw = ref<string>("");
const editModalDevice = ref<DeviceItem | null>(null);
const showPortal = ref(false);
const view = ref<"list" | "edit">("list");
const listLoading = ref(false);

const selectedDevice = computed(
  () => deviceItems.value.find((d) => d.hardware_id === selectedHw.value) ?? null
);
const selectedDeviceLat = computed(() => null as number | null);
const selectedDeviceLng = computed(() => null as number | null);
const deviceLabel = computed(
  () =>
    selectedDevice.value?.truck_name || selectedDevice.value?.label || selectedHw.value || "Device"
);

const msg = ref<string>("");
const msgIsError = ref(false);
const saving = ref(false);
const loading = ref(false);
const listError = ref<string>("");
let _loadSeq = 0;

const alarm = ref<AlarmSettings>({ ...alarmDefaults });
const trip = ref<TripSettings>({ ...tripDefaults });

const isEditing = ref(false);
const showConfirmBack = ref(false);
let savedAlarm: AlarmSettings = { ...alarmDefaults };
let savedTrip: TripSettings = { ...tripDefaults };

const { handleMouseOver, handleMouseLeave } = useRowHighlight();
const alarmRef = ref<InstanceType<typeof DsAlarm> | null>(null);

// ── Table columns ─────────────────────────────────────────────────────────────

const deviceColumns = [
  { name: "status", label: "Status", field: "ts", align: "left" as const, sortable: true },
  {
    name: "hardwareId",
    label: "Hardware ID",
    field: "hardware_id",
    align: "left" as const,
    sortable: true
  },
  { name: "truck", label: "Truck", field: "label", align: "left" as const, sortable: true },
  {
    name: "battery",
    label: "Battery",
    field: "battery_pct",
    align: "right" as const,
    sortable: true
  },
  { name: "lastSeen", label: "Last Active", field: "ts", align: "right" as const, sortable: true },
  { name: "actions", label: "Actions", field: "actions", align: "center" as const, sortable: false }
];

// ── Helpers ───────────────────────────────────────────────────────────────────

function statusBadge(item: DeviceItem) {
  return connectivityBadgeFromAge(item.ts);
}

function batteryClass(v: number | null): string {
  if (v == null) return "text-grey-5";
  if (v < 20) return "text-negative text-weight-bold";
  if (v < 40) return "text-orange-8 text-weight-bold";
  return "text-weight-bold";
}

function fmtAge(ts: string | null): string {
  if (!ts) return "—";
  const sec = Math.round((Date.now() - new Date(ts).getTime()) / 1000);
  if (sec < 0) return "Just now";
  const min = Math.round(sec / 60);
  if (min < 1) return "Just now";
  if (min < 60) return `${min}m ago`;
  const hr = Math.floor(min / 60);
  return `${hr}h ${min % 60}m ago`;
}

function ageClass(ts: string | null): string {
  if (!ts) return "text-grey-5";
  const sec = (Date.now() - new Date(ts).getTime()) / 1000;
  if (sec > 1800) return "text-grey-5";
  if (sec > 900) return "text-orange-8";
  return "";
}

function showMsg(text: string, isError = false) {
  msg.value = text;
  msgIsError.value = isError;
}

// ── Data loading ──────────────────────────────────────────────────────────────

async function loadDeviceList() {
  listLoading.value = true;
  listError.value = "";
  try {
    const res: any = await api.fleet.getDevicesSummary();
    deviceItems.value = (res?.details ?? []) as DeviceItem[];
  } catch (e: unknown) {
    listError.value = e instanceof Error ? e.message : String(e);
  } finally {
    listLoading.value = false;
  }
}

async function loadSettings() {
  if (!selectedHw.value) {
    showMsg("No device selected.", true);
    return;
  }
  const seq = ++_loadSeq;
  loading.value = true;
  msg.value = "";
  try {
    const result = await api.fleet.getDeviceSettings(selectedHw.value);
    if (seq !== _loadSeq) return;
    const raw = result as any;
    const details = raw?.details as any;
    const row = details?.row as any;
    const found = details?.found ?? !!row;

    if (!found || !row) {
      alarm.value = { ...alarmDefaults };
      trip.value = { ...tripDefaults };
      showMsg(`No saved settings for ${selectedHw.value} — showing defaults.`);
      return;
    }

    alarm.value = normalizeAlarm(row.alarm_json ?? {});
    trip.value = normalizeTrip(row.trip_json ?? {});
    showMsg(`Settings loaded for ${selectedHw.value}`);
  } catch (e: unknown) {
    showMsg(e instanceof Error ? e.message : String(e), true);
  } finally {
    loading.value = false;
  }
}

async function saveSettings() {
  if (!selectedHw.value) {
    showMsg("No device selected.", true);
    return;
  }
  if (alarmRef.value?.hasErrors) {
    showMsg("Fix validation errors before saving.", true);
    return;
  }
  saving.value = true;
  msg.value = "";
  try {
    await api.fleet.saveDeviceSettings({
      hardware_id: selectedHw.value,
      alarm_json: serializeAlarm(toRaw(alarm.value)),
      trip_json: serializeTrip(toRaw(trip.value))
    });
    showMsg("Settings saved successfully.");
    isEditing.value = false;
    await loadSettings();
    await loadDeviceList();
  } catch (e: any) {
    const errMsg = e?.response?.data?.message ?? (e instanceof Error ? e.message : String(e));
    showMsg(errMsg, true);
  } finally {
    saving.value = false;
  }
}

// ── Navigation ────────────────────────────────────────────────────────────────

async function editSettings(hw: string) {
  selectedHw.value = hw;
  view.value = "edit";
  isEditing.value = false;
  msg.value = "";
  alarm.value = { ...alarmDefaults };
  trip.value = { ...tripDefaults };
  await loadSettings();
}

function backToList() {
  view.value = "list";
  isEditing.value = false;
  msg.value = "";
}

function handleBack() {
  showConfirmBack.value = true;
}

function enterEdit() {
  savedAlarm = { ...alarm.value };
  savedTrip = { ...trip.value };
  isEditing.value = true;
}

function cancelEdit() {
  alarm.value = { ...savedAlarm };
  trip.value = { ...savedTrip };
  isEditing.value = false;
  msg.value = "";
}

// Guard browser back / sidebar navigation while in edit mode with unsaved changes
onBeforeRouteLeave((_to, _from, next) => {
  if (view.value === "edit" && isEditing.value) {
    showConfirmBack.value = true;
    // Store the guard callback so confirmLeave can call it
    _pendingNavNext = next;
  } else {
    next();
  }
});
let _pendingNavNext: ((ok?: boolean) => void) | null = null;
function confirmLeaveAndNav() {
  showConfirmBack.value = false;
  if (_pendingNavNext) {
    _pendingNavNext();
    _pendingNavNext = null;
  } else backToList();
}
function stayOnPage() {
  showConfirmBack.value = false;
  if (_pendingNavNext) {
    _pendingNavNext(false);
    _pendingNavNext = null;
  }
}

// ── Registration ──────────────────────────────────────────────────────────────

async function onDeviceRegistered(hardwareId: string) {
  showPortal.value = false;
  await loadDeviceList();
  if (hardwareId) await editSettings(hardwareId);
}

onMounted(loadDeviceList);
</script>

<template>
  <div>
    <DsHeader @add-device="showPortal = true" />

    <!-- ── LIST VIEW ───────────────────────────────────────────────────────── -->
    <template v-if="view === 'list'">
      <div class="app-container q-mx-lg q-mt-lg">
        <q-banner
          v-if="listError"
          dense
          rounded
          class="bg-red-1 text-red-9 q-mb-md"
          style="font-size: 13px"
        >
          <template #avatar>
            <q-icon name="fa-solid fa-circle-xmark" color="negative" size="14px" />
          </template>
          {{ listError }}
        </q-banner>

        <q-table
          :rows="deviceItems"
          :columns="deviceColumns"
          row-key="hardware_id"
          :loading="listLoading"
          flat
          separator="cell"
          :rows-per-page-options="[25, 50, 100, 0]"
          rows-per-page-label="Rows per page"
          :class="$style.styledTable"
          @mouseover="handleMouseOver"
          @mouseleave="handleMouseLeave"
        >
          <!-- Status -->
          <template #body-cell-status="{ row }">
            <q-td auto-width>
              <q-badge
                :color="statusBadge(row).color"
                :text-color="statusBadge(row).textColor"
                class="q-px-sm text-caption"
              >
                <q-icon :name="statusBadge(row).icon" size="11px" class="q-mr-xs" />
                {{ statusBadge(row).label }}
              </q-badge>
            </q-td>
          </template>

          <!-- Truck -->
          <template #body-cell-truck="{ row }">
            <q-td auto-width>
              <span v-if="row.truck_name || row.label">{{ row.truck_name || row.label }}</span>
              <span v-else class="text-grey-5 text-italic">Unlabelled</span>
            </q-td>
          </template>

          <!-- Battery -->
          <template #body-cell-battery="{ row }">
            <q-td auto-width class="text-right">
              <span :class="batteryClass(row.battery_pct)">
                {{ row.battery_pct != null ? `${Math.round(row.battery_pct)}%` : "—" }}
                <q-icon
                  v-if="row.battery_pct != null && row.battery_pct < 20"
                  name="fa-solid fa-triangle-exclamation"
                  size="11px"
                  class="q-ml-xs"
                />
              </span>
            </q-td>
          </template>

          <!-- Last Active -->
          <template #body-cell-lastSeen="{ row }">
            <q-td auto-width class="text-right">
              <span :class="ageClass(row.ts)">{{ fmtAge(row.ts) }}</span>
            </q-td>
          </template>

          <!-- Actions -->
          <template #body-cell-actions="{ row }">
            <q-td auto-width class="text-center">
              <q-btn
                flat
                round
                dense
                icon="fa-solid fa-tag"
                color="grey-7"
                size="sm"
                @click.stop="editModalDevice = row"
              >
                <q-tooltip>Edit device info</q-tooltip>
              </q-btn>
              <q-btn
                flat
                round
                dense
                icon="fa-solid fa-pen-to-square"
                color="grey-7"
                size="sm"
                @click.stop="editSettings(row.hardware_id)"
              >
                <q-tooltip>Edit alarm &amp; trip settings</q-tooltip>
              </q-btn>
            </q-td>
          </template>

          <template #no-data>
            <div class="full-width text-center q-pa-lg text-grey-5 text-body1">
              <q-icon name="fa-solid fa-satellite-dish" size="40px" class="q-mb-sm" />
              <br />
              No devices registered — click
              <strong>Add Device</strong>
              to get started.
            </div>
          </template>
        </q-table>
      </div>
    </template>

    <!-- ── EDIT VIEW (alarm / trip settings) ──────────────────────────────── -->
    <template v-else>
      <div class="app-container q-mx-lg q-mt-lg">
        <div class="row bg-white">
          <DsDeviceSelect
            :device-label="deviceLabel"
            :saving="saving"
            :msg="msg"
            :msg-is-error="msgIsError"
            :is-editing="isEditing"
            @save="saveSettings"
            @edit="enterEdit"
            @cancel="cancelEdit"
            @back="handleBack"
          />

          <div style="width: 100%">
            <q-card flat>
              <q-card-section class="headerColor text-weight-bold">
                <q-icon name="fa-solid fa-bell" size="14px" class="q-mr-xs" />
                Alarm Settings
              </q-card-section>
              <q-separator />
              <q-card-section>
                <DsAlarm ref="alarmRef" v-model="alarm" :disable="!isEditing" />
              </q-card-section>
            </q-card>

            <q-separator />

            <q-card flat>
              <q-card-section class="headerColor text-weight-bold">
                <q-icon name="fa-solid fa-bell" size="14px" class="q-mr-xs" />
                Notifications
              </q-card-section>
              <q-separator />
              <DsNotification
                :model-value="{
                  notify_email: alarm.notify_email,
                  email_enabled: alarm.email_enabled,
                  debounce_count: alarm.debounce_count,
                  email_cooldown_minutes: alarm.email_cooldown_minutes
                }"
                :disable="!isEditing"
                @update:model-value="
                  (v: NotificationSettings) => {
                    alarm.notify_email = v.notify_email;
                    alarm.email_enabled = v.email_enabled;
                    alarm.debounce_count = v.debounce_count;
                    alarm.email_cooldown_minutes = v.email_cooldown_minutes;
                  }
                "
              />
            </q-card>

            <q-separator />

            <q-card flat>
              <q-card-section class="headerColor text-weight-bold">
                <q-icon name="fa-solid fa-truck" size="14px" class="q-mr-xs" />
                Trip Settings
              </q-card-section>
              <q-separator />
              <q-card-section>
                <DsTrip
                  v-model="trip"
                  :device-lat="selectedDeviceLat"
                  :device-lng="selectedDeviceLng"
                  :disable="!isEditing"
                />
              </q-card-section>
            </q-card>

            <q-separator />

            <q-card flat>
              <q-card-section class="headerColor text-weight-bold">
                <q-icon name="fa-solid fa-location-dot" size="14px" class="q-mr-xs" />
                Location Library
              </q-card-section>
              <q-separator />
              <DsLocations :hardware-id="selectedHw" />
            </q-card>
          </div>
        </div>
      </div>
    </template>
  </div>

  <!-- Device activation portal -->
  <FleetDevicePortal
    v-if="showPortal"
    @registered="(hw: string) => onDeviceRegistered(hw)"
    @close="showPortal = false"
  />

  <!-- Edit device info modal (label, device_int_id, credentials) -->
  <DeviceEditModal
    v-if="editModalDevice"
    :device="editModalDevice"
    @saved="editModalDevice = null; loadDeviceList()"
    @close="editModalDevice = null"
  />

  <!-- Unsaved settings confirm dialog -->
  <q-dialog v-model="showConfirmBack" persistent>
    <q-card style="min-width: 320px">
      <q-card-section class="q-pa-lg">
        <div class="text-h6 text-weight-bold">Leave Device Settings?</div>
        <div class="text-body2 q-mt-md">Unsaved alarm and trip changes will be lost.</div>
      </q-card-section>
      <q-card-actions align="right" class="q-pb-lg q-pr-lg q-gutter-sm">
        <q-btn flat no-caps label="Stay" color="primary" @click="stayOnPage" />
        <q-btn flat no-caps label="Leave" color="primary" @click="confirmLeaveAndNav" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<style lang="sass" module>
@import '../../../../style/_variables'

.styledTable
  :global(thead tr th)
    font-weight: 700 !important
    font-size: 14px !important
    color: $primary-black !important
    text-transform: capitalize !important
    background-image: linear-gradient(0deg, $secondary-grey-2 0%, $white 100%) !important
    box-sizing: border-box !important
    border-top: 1px solid $secondary-grey-2 !important
  :global(tbody tr td)
    padding: 8px !important
    font-size: 14px !important
    color: $primary-black !important
</style>
