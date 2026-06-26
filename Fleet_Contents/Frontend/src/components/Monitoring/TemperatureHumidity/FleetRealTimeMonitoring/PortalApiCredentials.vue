<script setup lang="ts">
import { ref } from "vue";

export interface ApiCredentials {
  appId: string;
  appKey: string;
  appSecret: string;
}

const props = defineProps<{
  modelValue: ApiCredentials;
  loading: boolean;
}>();

const emit = defineEmits<{
  (e: "update:modelValue", v: ApiCredentials): void;
  (e: "enter"): void;
}>();

const showSecret = ref(false);

function update(field: keyof ApiCredentials, value: string) {
  emit("update:modelValue", { ...props.modelValue, [field]: value });
}
</script>

<template>
  <div class="text-caption text-grey-6 q-mb-sm q-mt-xs">
    <q-icon name="fa-solid fa-key" size="12px" class="q-mr-xs" />
    API Credentials (from Fleet cloud API Authorization)
  </div>

  <q-input
    :model-value="props.modelValue.appId"
    label="App ID"
    placeholder="e.g. c24599ae73004dbfabdf4062dea0ad8b"
    outlined
    dense
    class="q-mb-md"
    :disable="props.loading"
    @update:model-value="(v) => update('appId', String(v ?? ''))"
    @keyup.enter="emit('enter')"
  >
    <template #prepend>
      <q-icon name="fa-solid fa-fingerprint" color="primary" size="16px" />
    </template>
  </q-input>

  <q-input
    :model-value="props.modelValue.appKey"
    label="App Key"
    placeholder="e.g. User-6100"
    outlined
    dense
    class="q-mb-md"
    :disable="props.loading"
    @update:model-value="(v) => update('appKey', String(v ?? ''))"
    @keyup.enter="emit('enter')"
  >
    <template #prepend><q-icon name="fa-solid fa-id-badge" color="primary" size="16px" /></template>
  </q-input>

  <q-input
    :model-value="props.modelValue.appSecret"
    label="App Secret"
    placeholder="Enter App Secret"
    outlined
    dense
    class="q-mb-md"
    :type="showSecret ? 'text' : 'password'"
    :disable="props.loading"
    @update:model-value="(v) => update('appSecret', String(v ?? ''))"
    @keyup.enter="emit('enter')"
  >
    <template #prepend>
      <q-icon name="fa-solid fa-shield-halved" color="primary" size="16px" />
    </template>
    <template #append>
      <q-icon
        :name="showSecret ? 'visibility_off' : 'visibility'"
        class="cursor-pointer"
        @click="showSecret = !showSecret"
      />
    </template>
  </q-input>
</template>
