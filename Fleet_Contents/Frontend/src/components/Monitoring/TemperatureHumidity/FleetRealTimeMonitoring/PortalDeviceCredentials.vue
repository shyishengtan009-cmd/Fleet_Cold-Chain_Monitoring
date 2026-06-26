<script setup lang="ts">
import { ref } from "vue";

export interface DeviceCredentials {
  deviceId: string;
  code: string;
  label: string;
}

const props = defineProps<{
  modelValue: DeviceCredentials;
  loading: boolean;
}>();

const emit = defineEmits<{
  (e: "update:modelValue", v: DeviceCredentials): void;
  (e: "enter"): void;
}>();

const showCode = ref(false);

function update(field: keyof DeviceCredentials, value: string) {
  emit("update:modelValue", { ...props.modelValue, [field]: value });
}
</script>

<template>
  <q-input
    :model-value="props.modelValue.deviceId"
    label="Device ID"
    placeholder="e.g. 190026010000218"
    outlined
    dense
    class="q-mb-md"
    :disable="props.loading"
    @update:model-value="(v) => update('deviceId', String(v ?? ''))"
    @keyup.enter="emit('enter')"
  >
    <template #prepend>
      <q-icon name="fa-solid fa-microchip" color="primary" size="16px" />
    </template>
  </q-input>

  <q-input
    :model-value="props.modelValue.code"
    label="Activation Code"
    placeholder="Enter code from device label"
    outlined
    dense
    class="q-mb-md"
    :type="showCode ? 'text' : 'password'"
    :disable="props.loading"
    @update:model-value="(v) => update('code', String(v ?? ''))"
    @keyup.enter="emit('enter')"
  >
    <template #prepend><q-icon name="fa-solid fa-key" color="primary" size="16px" /></template>
    <template #append>
      <q-icon
        :name="showCode ? 'visibility_off' : 'visibility'"
        class="cursor-pointer"
        @click="showCode = !showCode"
      />
    </template>
  </q-input>

  <q-input
    :model-value="props.modelValue.label"
    label="Device Label (optional)"
    placeholder="e.g. Cold Storage Unit 1"
    outlined
    dense
    class="q-mb-md"
    :disable="props.loading"
    @update:model-value="(v) => update('label', String(v ?? ''))"
    @keyup.enter="emit('enter')"
  >
    <template #prepend><q-icon name="fa-solid fa-tag" color="grey-6" size="16px" /></template>
  </q-input>
</template>
