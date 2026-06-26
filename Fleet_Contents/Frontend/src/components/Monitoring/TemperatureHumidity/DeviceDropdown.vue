<script setup lang="ts">
import { ref, computed, watch } from "vue";

const selectRef = ref<{ hidePopup: () => void } | null>(null);

function onScroll() {
  selectRef.value?.hidePopup();
}
function onPopupShow() {
  window.addEventListener("scroll", onScroll, true);
}
function onPopupHide() {
  window.removeEventListener("scroll", onScroll, true);
}

export interface DeviceTab {
  hardwareId: string;
  truckName?: string | null;
  status?: string;
}

const props = defineProps<{
  modelValue: string;
  devices: DeviceTab[];
}>();

const emit = defineEmits<{
  (e: "update:modelValue", v: string): void;
}>();

const filteredOptions = ref<DeviceTab[]>([]);

watch(
  () => props.devices,
  (devices) => {
    filteredOptions.value = [...devices];
  },
  { immediate: true }
);

const selectedOption = computed(
  () => props.devices.find((d) => d.hardwareId === props.modelValue) ?? null
);

function label(d: DeviceTab | null | undefined): string {
  if (!d) return "";
  return d.truckName?.trim() ? d.truckName.trim() : d.hardwareId;
}

function dotColor(status: string | undefined): string {
  if (status === "OK") return "#43a047";
  if (status === "WARN") return "#fb8c00";
  if (status === "OFFLINE") return "#e53935";
  return "#bdbdbd";
}

function filterFn(val: string, update: (fn: () => void) => void) {
  update(() => {
    if (!val) {
      filteredOptions.value = props.devices;
    } else {
      const q = val.toLowerCase();
      filteredOptions.value = props.devices.filter(
        (d) =>
          (d.truckName ?? "").toLowerCase().includes(q) ||
          d.hardwareId.toLowerCase().includes(q)
      );
    }
  });
}
</script>

<template>
  <q-select
    ref="selectRef"
    :model-value="selectedOption"
    :options="filteredOptions"
    :option-label="label"
    outlined
    dense
    use-input
    input-debounce="0"
    placeholder="Search by name or ID…"
    style="min-width: 260px; max-width: 420px"
    @filter="filterFn"
    @update:model-value="(opt) => emit('update:modelValue', opt?.hardwareId ?? '')"
    @popup-show="onPopupShow"
    @popup-hide="onPopupHide"
  >
    <!-- Selected value display -->
    <template #selected-item="scope">
      <div class="row items-center no-wrap">
        <div
          class="q-mr-sm"
          style="width: 9px; height: 9px; border-radius: 50%; flex-shrink: 0"
          :style="{ backgroundColor: dotColor(scope.opt?.status) }"
        />
        <span>{{ label(scope.opt) }}</span>
      </div>
    </template>

    <!-- Option row in dropdown -->
    <template #option="scope">
      <q-item v-bind="scope.itemProps">
        <q-item-section side style="padding-right: 10px; min-width: unset">
          <div
            style="width: 10px; height: 10px; border-radius: 50%"
            :style="{ backgroundColor: dotColor(scope.opt.status) }"
          />
        </q-item-section>
        <q-item-section>
          <q-item-label>{{ label(scope.opt) }}</q-item-label>
          <q-item-label v-if="scope.opt.truckName?.trim()" caption class="text-grey-6">
            {{ scope.opt.hardwareId }}
          </q-item-label>
        </q-item-section>
      </q-item>
    </template>

    <!-- Empty search result -->
    <template #no-option="{ inputValue }">
      <q-item>
        <q-item-section class="text-grey-5 text-italic">
          No device matching "{{ inputValue }}"
        </q-item-section>
      </q-item>
    </template>
  </q-select>
</template>
