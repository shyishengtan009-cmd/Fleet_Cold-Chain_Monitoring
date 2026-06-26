<script setup lang="ts">
import { ref, watch } from "vue";
import { useQuasar } from "quasar";
import api from "@/helpers/api";

export interface DeviceSummaryItem {
  hardware_id: string;
  label: string | null;
  device_int_id: number | null;
  has_polling: boolean;
  has_custom_creds: boolean;
}

const props = defineProps<{ device: DeviceSummaryItem }>();
const emit = defineEmits<{ (e: "saved"): void; (e: "close"): void }>();

const $q = useQuasar();
const saving = ref(false);

// ── Form state ─────────────────────────────────────────────────────────────

const label       = ref("");
const deviceIntId = ref<string>("");  // string so q-input works cleanly; parsed on save
const appId       = ref("");
const appKey      = ref("");
const appSecret   = ref("");
const clearCreds  = ref(false);
const showSecret  = ref(false);

watch(
  () => props.device,
  (d) => {
    label.value       = d.label ?? "";
    deviceIntId.value = d.device_int_id != null ? String(d.device_int_id) : "";
    appId.value       = "";
    appKey.value      = "";
    appSecret.value   = "";
    clearCreds.value  = false;
    showSecret.value  = false;
  },
  { immediate: true }
);

// ── Validation ─────────────────────────────────────────────────────────────

function deviceIntIdError(): string | true {
  if (deviceIntId.value.trim() === "") return true; // blank = clear = allowed
  const n = Number(deviceIntId.value);
  if (!Number.isInteger(n) || n < 1) return "Must be a positive integer";
  return true;
}

function canSave(): boolean {
  if (deviceIntId.value.trim() !== "" && deviceIntIdError() !== true) return false;
  return true;
}

// ── Save ───────────────────────────────────────────────────────────────────

async function save() {
  if (!canSave()) return;

  const payload: Record<string, unknown> = {};

  // Label: always send (empty string = clear)
  payload.label = label.value.trim() || null;

  // device_int_id: send if changed
  const rawId = deviceIntId.value.trim();
  payload.device_int_id = rawId === "" ? null : Number(rawId);

  // Credentials: only include if user typed something (or clearing)
  if (clearCreds.value) {
    payload.clear_credentials = true;
  } else {
    if (appId.value.trim())     payload.app_id     = appId.value.trim();
    if (appKey.value.trim())    payload.app_key    = appKey.value.trim();
    if (appSecret.value.trim()) payload.app_secret = appSecret.value.trim();
  }

  saving.value = true;
  try {
    await api.fleet.updateDevice(props.device.hardware_id, payload);
    $q.notify({ type: "positive", message: "Device updated.", position: "top-right", timeout: 3000 });
    emit("saved");
  } catch (e: any) {
    $q.notify({
      type: "negative",
      message: e?.response?.data?.message ?? "Failed to update device.",
      position: "top-right",
      timeout: 6000
    });
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <q-dialog :model-value="true" persistent @keyup.esc="emit('close')">
    <q-card style="width: 480px; max-width: 95vw">
      <!-- Header -->
      <q-card-section class="row items-center q-pb-none">
        <div class="text-h6 text-weight-bold">Edit Device</div>
        <q-space />
        <q-btn flat round dense icon="close" :disable="saving" @click="emit('close')" />
      </q-card-section>

      <q-card-section class="q-pt-xs q-pb-none">
        <div class="text-caption text-grey-6">
          <q-icon name="fa-solid fa-microchip" size="12px" class="q-mr-xs" />
          {{ props.device.hardware_id }}
        </div>
      </q-card-section>

      <q-separator class="q-mt-sm" />

      <q-card-section class="q-gutter-md">
        <!-- Label -->
        <q-input
          v-model="label"
          label="Device Label"
          outlined
          dense
          :disable="saving"
        >
          <template #prepend>
            <q-icon name="fa-solid fa-tag" color="grey-6" size="14px">
              <q-tooltip>Display name shown throughout Fleet</q-tooltip>
            </q-icon>
          </template>
        </q-input>

        <!-- Device Int ID -->
        <q-input
          v-model="deviceIntId"
          label="TZone Device Integer ID"
          outlined
          dense
          :disable="saving"
          :rules="[() => deviceIntIdError()]"
          lazy-rules
        >
          <template #prepend>
            <q-icon name="fa-solid fa-hashtag" color="grey-6" size="14px">
              <q-tooltip>
                Required for data polling. Find via TZone API: GET /Device?key={"{hardware_id}"}
              </q-tooltip>
            </q-icon>
          </template>
          <template #append>
            <q-icon
              v-if="!props.device.has_polling"
              name="fa-solid fa-triangle-exclamation"
              color="warning"
              size="14px"
            >
              <q-tooltip>Not set — device will not be polled for data</q-tooltip>
            </q-icon>
            <q-icon v-else name="fa-solid fa-circle-check" color="positive" size="14px">
              <q-tooltip>Polling active</q-tooltip>
            </q-icon>
          </template>
        </q-input>
      </q-card-section>

      <q-separator />

      <!-- TZone Credentials section -->
      <q-card-section>
        <div class="row items-center q-mb-sm">
          <div class="text-caption text-weight-bold text-grey-7" style="text-transform:uppercase;font-size:11px">
            TZone API Credentials
          </div>
          <q-space />
          <q-badge
            :color="props.device.has_custom_creds ? 'blue-1' : 'grey-2'"
            :text-color="props.device.has_custom_creds ? 'blue-8' : 'grey-6'"
            class="text-caption"
          >
            {{ props.device.has_custom_creds ? "Per-device" : "Using global" }}
          </q-badge>
        </div>

        <q-banner dense rounded class="bg-blue-1 text-blue-9 q-mb-md" style="font-size:12px">
          <template #avatar><q-icon name="fa-solid fa-circle-info" color="primary" size="12px" /></template>
          Leave fields blank to keep existing values. Fill all three to switch to per-device credentials.
        </q-banner>

        <div class="q-gutter-sm">
          <q-input
            v-model="appId"
            label="App ID"
            outlined
            dense
            :disable="saving || clearCreds"
            placeholder="leave blank to keep existing"
          />
          <q-input
            v-model="appKey"
            label="App Key"
            outlined
            dense
            :disable="saving || clearCreds"
            placeholder="leave blank to keep existing"
          />
          <q-input
            v-model="appSecret"
            label="App Secret"
            outlined
            dense
            :type="showSecret ? 'text' : 'password'"
            :disable="saving || clearCreds"
            placeholder="leave blank to keep existing"
          >
            <template #append>
              <q-icon
                :name="showSecret ? 'visibility_off' : 'visibility'"
                class="cursor-pointer"
                @click="showSecret = !showSecret"
              />
            </template>
          </q-input>

          <q-toggle
            v-model="clearCreds"
            label="Clear credentials (revert to global)"
            color="negative"
            dense
            :disable="saving || !props.device.has_custom_creds"
          />
        </div>
      </q-card-section>

      <q-separator />

      <q-card-actions align="right" class="q-pa-md q-gutter-sm">
        <q-btn flat no-caps label="Cancel" color="grey-7" :disable="saving" @click="emit('close')" />
        <q-btn
          unelevated
          no-caps
          label="Save Changes"
          color="primary"
          :loading="saving"
          :disable="!canSave()"
          @click="save"
        />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>
