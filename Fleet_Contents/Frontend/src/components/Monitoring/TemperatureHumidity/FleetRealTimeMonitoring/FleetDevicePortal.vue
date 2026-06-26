<script setup lang="ts">
import { ref, onMounted } from "vue";
import { useQuasar } from "quasar";
import api from "@/helpers/api";
import PortalDeviceList from "./PortalDeviceList.vue";
import PortalActivationForm from "./PortalActivationForm.vue";
import type { ActivationFormData } from "./PortalActivationForm.vue";

const emit = defineEmits<{ (e: "registered", hardwareId: string): void; (e: "close"): void }>();
const $q = useQuasar();

interface RegisteredDevice {
  hardware_id: string;
  label: string | null;
  registered_at: string | null;
}

const devices = ref<RegisteredDevice[]>([]);
const devicesLoading = ref(false);
const removingId = ref<string | null>(null);
const loading = ref(false);
const error = ref<string | null>(null);

async function loadDevices() {
  devicesLoading.value = true;
  try {
    const res: any = await api.fleet.getDevices();
    devices.value = res?.details ?? [];
  } catch (e: any) {
    devices.value = [];
    $q.notify({
      type: "negative",
      message: "Failed to load devices.",
      caption: String(e?.response?.data?.message ?? e?.message ?? "Check network connection."),
      timeout: 6000,
      position: "top-right"
    });
  } finally {
    devicesLoading.value = false;
  }
}

async function removeDevice(hw: string) {
  removingId.value = hw;
  try {
    await api.fleet.unregisterDevice(hw);
    await loadDevices();
  } catch (e: any) {
    $q.notify({
      type: "negative",
      message: `Failed to remove device ${hw}.`,
      caption: String(
        e?.response?.data?.message ?? e?.message ?? "The device may still be registered."
      ),
      timeout: 6000,
      position: "top-right"
    });
  } finally {
    removingId.value = null;
  }
}

async function onFormSubmit(data: ActivationFormData) {
  error.value = null;
  loading.value = true;
  try {
    await api.fleet.registerDevice(
      data.hw,
      data.code,
      data.label,
      data.appId,
      data.appKey,
      data.appSecret
    );
    emit("registered", data.hw);
  } catch (e: any) {
    error.value =
      typeof e === "string"
        ? e
        : e?.response?.data?.message ??
          "Invalid Device ID or Activation Code. Please check the label on your device.";
  } finally {
    loading.value = false;
  }
}

onMounted(loadDevices);
</script>

<template>
  <div class="fleet-portal-backdrop" @click.self="emit('close')">
    <q-card class="fleet-portal-card">
      <div class="fleet-portal-header">
        <q-icon name="fa-solid fa-satellite-dish" size="40px" color="white" />
        <div class="fleet-portal-header-text">
          <div class="text-h6 text-white text-weight-bold">Fleet Device Activation</div>
          <div class="text-caption text-white" style="opacity: 0.85">
            Connect your Fleet sensor to your account
          </div>
        </div>
        <q-space />
        <q-btn
          flat
          round
          dense
          icon="close"
          color="white"
          class="portal-close-btn"
          :disable="loading"
          @click="emit('close')"
        />
      </div>

      <div class="portal-scroll-body">
        <PortalDeviceList
          :devices="devices"
          :devices-loading="devicesLoading"
          :removing-id="removingId"
          @remove="removeDevice"
        />

        <q-separator class="q-mx-lg" />

        <PortalActivationForm :loading="loading" :error="error" @submit="onFormSubmit" />

        <q-card-section class="q-pt-none q-px-lg q-pb-lg">
          <div class="text-caption text-grey-6 text-center">
            <q-icon name="fa-solid fa-circle-info" size="12px" class="q-mr-xs" />
            The Device ID and Activation Code are on the sticker attached to your Fleet device.
          </div>
        </q-card-section>
      </div>
    </q-card>
  </div>
</template>

<style scoped>
.fleet-portal-backdrop {
  position: fixed;
  inset: 0;
  z-index: 9000;
  background: rgba(0, 0, 0, 0.55);
  backdrop-filter: blur(4px);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 16px;
}
.fleet-portal-card {
  width: 100%;
  max-width: 440px;
  max-height: 90vh;
  border-radius: 16px;
  overflow: hidden;
  box-shadow: 0 24px 60px rgba(0, 0, 0, 0.35);
  display: flex;
  flex-direction: column;
}
.portal-scroll-body {
  overflow-y: auto;
  flex: 1;
}
.fleet-portal-header {
  background: linear-gradient(135deg, #1976d2, #0d47a1);
  padding: 24px;
  display: flex;
  align-items: center;
  gap: 16px;
}
.fleet-portal-header-text {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.portal-close-btn {
  opacity: 1;
  background: rgba(255, 255, 255, 0.15);
  border-radius: 50%;
}
.portal-close-btn:hover {
  background: rgba(255, 255, 255, 0.3);
}
</style>
