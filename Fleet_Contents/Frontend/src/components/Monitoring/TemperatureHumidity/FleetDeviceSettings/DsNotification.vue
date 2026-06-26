<script setup lang="ts">
import { computed } from "vue";

import LabelApp from "@/components/common/LabelApp.vue";

export type NotificationSettings = {
  notify_email: string;
  email_enabled: boolean;
  debounce_count: number | null;
  email_cooldown_minutes: number | null;
};

const props = defineProps<{ modelValue: NotificationSettings; disable?: boolean }>();
const emit = defineEmits<{ (e: "update:modelValue", v: NotificationSettings): void }>();

const model = computed<NotificationSettings>({
  get: () => props.modelValue,
  set: (v) => emit("update:modelValue", v)
});

function update<K extends keyof NotificationSettings>(key: K, value: NotificationSettings[K]) {
  model.value = { ...model.value, [key]: value };
}

function parseNumber(v: unknown): number | null {
  if (v === "" || v === null || v === undefined) return null;
  const n = Number(v);
  return Number.isFinite(n) ? n : null;
}
</script>

<template>
  <div class="q-pa-md">
    <!-- Row 1: email input -->
    <div class="row q-col-gutter-sm items-end q-mb-sm">
      <LabelApp label="Alert Email" tooltip="Separate multiple addresses with commas" class="col col-12 col-sm-6 col-md-5">
        <q-input
          dense
          outlined
          placeholder="email@example.com, manager@example.com"
          :disable="props.disable"
          :model-value="model.notify_email"
          @update:model-value="(v) => update('notify_email', String(v || ''))"
        />
      </LabelApp>
    </div>

    <!-- Row 2: email toggle -->
    <div class="row q-col-gutter-sm items-center q-mb-md">
      <div class="col col-12 col-sm-4 col-md-4">
        <q-toggle
          dense
          label="Enable Email Alerts"
          :disable="props.disable"
          :model-value="model.email_enabled"
          @update:model-value="(v) => update('email_enabled', !!v)"
        />
      </div>
    </div>

    <!-- Row 3: email config -->
    <div class="row q-col-gutter-sm items-end">
      <LabelApp label="Debounce Count" tooltip="Number of consecutive out-of-range readings before an email is sent" class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          type="number"
          :disable="props.disable"
          :model-value="model.debounce_count"
          @update:model-value="(v) => update('debounce_count', parseNumber(v))"
        />
      </LabelApp>
      <LabelApp label="Email Cooldown (min)" tooltip="Minimum minutes between repeated alert emails for the same device" class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          type="number"
          :disable="props.disable"
          :model-value="model.email_cooldown_minutes"
          @update:model-value="(v) => update('email_cooldown_minutes', parseNumber(v))"
        />
      </LabelApp>
    </div>
  </div>
</template>
