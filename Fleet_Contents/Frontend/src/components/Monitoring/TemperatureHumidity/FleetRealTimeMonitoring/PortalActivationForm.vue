<script setup lang="ts">
import { ref } from "vue";
import PortalDeviceCredentials from "./PortalDeviceCredentials.vue";
import PortalApiCredentials from "./PortalApiCredentials.vue";
import type { DeviceCredentials } from "./PortalDeviceCredentials.vue";
import type { ApiCredentials } from "./PortalApiCredentials.vue";

export interface ActivationFormData {
  hw: string;
  code: string;
  label?: string;
  appId?: string;
  appKey?: string;
  appSecret?: string;
}

const props = defineProps<{
  loading: boolean;
  error: string | null;
}>();

const emit = defineEmits<{ (e: "submit", data: ActivationFormData): void }>();

const device = ref<DeviceCredentials>({ deviceId: "", code: "", label: "" });
const api = ref<ApiCredentials>({ appId: "", appKey: "", appSecret: "" });

function submit() {
  const hw = device.value.deviceId.trim().toUpperCase();
  const code = device.value.code.trim();
  if (!hw || !code) return;
  emit("submit", {
    hw,
    code,
    label: device.value.label.trim() || undefined,
    appId: api.value.appId.trim() || undefined,
    appKey: api.value.appKey.trim() || undefined,
    appSecret: api.value.appSecret.trim() || undefined
  });
}
</script>

<template>
  <q-card-section class="q-pt-lg q-px-lg">
    <p class="text-body2 text-grey-7 q-mb-lg">
      Enter the
      <strong>Device ID</strong>
      and
      <strong>Activation Code</strong>
      printed on the label of your Fleet device. Optionally provide the
      <strong>API credentials</strong>
      from the Fleet cloud API Authorization page for per-device data polling.
    </p>

    <PortalDeviceCredentials v-model="device" :loading="props.loading" @enter="submit" />
    <PortalApiCredentials v-model="api" :loading="props.loading" @enter="submit" />

    <q-banner v-if="props.error" class="bg-red-1 text-red-9 q-mb-md rounded-borders" dense>
      <template #avatar><q-icon name="fa-solid fa-circle-exclamation" color="negative" /></template>
      {{ props.error }}
    </q-banner>

    <q-btn
      label="Activate Device"
      color="primary"
      unelevated
      no-caps
      class="full-width q-py-sm"
      :loading="props.loading"
      @click="submit"
    />
  </q-card-section>
</template>
