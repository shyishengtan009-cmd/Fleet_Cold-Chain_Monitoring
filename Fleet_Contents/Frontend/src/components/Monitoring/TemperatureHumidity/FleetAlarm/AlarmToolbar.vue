<script setup lang="ts">
import { ref } from "vue";
import { QSelectOption } from "quasar";

const selectRef = ref<{ hidePopup: () => void } | null>(null);
function onScroll() { selectRef.value?.hidePopup(); }
function onShow() { window.addEventListener("scroll", onScroll, true); }
function onHide() { window.removeEventListener("scroll", onScroll, true); }

type DeviceOption = { label: string; value: string };

const props = defineProps<{
  deviceOptions: DeviceOption[];
  selectedHw: string;
  saving: boolean;
  msg: string;
  msgIsError: boolean;
}>();

const emit = defineEmits<{
  (e: "update:selectedHw", value: string): void;
  (e: "load"): void;
  (e: "save"): void;
  (e: "refresh"): void;
}>();
</script>

<template>
  <div class="q-mx-lg q-mt-lg">
    <div class="row items-center q-col-gutter-md">
      <div class="col-12 col-md-5">
        <q-select
          ref="selectRef"
          dense
          outlined
          :model-value="props.selectedHw"
          :options="props.deviceOptions"
          option-label="label"
          option-value="value"
          emit-value
          map-options
          label="Device"
          @update:model-value="
            (v: QSelectOption | string | null) => emit('update:selectedHw', String(v ?? ''))
          "
          @popup-show="onShow"
          @popup-hide="onHide"
        />
      </div>

      <div class="col-auto">
        <q-btn dense no-caps color="primary" outline label="Load" @click="emit('load')" />
      </div>

      <div class="col-auto">
        <q-btn
          dense
          no-caps
          color="primary"
          label="Save"
          :loading="props.saving"
          @click="emit('save')"
        />
      </div>

      <div class="col-auto">
        <q-btn outline no-caps color="white text-black" @click="emit('refresh')">
          <q-icon class="q-pr-xs" size="13px" name="fa-solid fa-arrows-rotate" />
          Refresh
        </q-btn>
      </div>

      <div class="col">
        <q-banner
          v-if="props.msg"
          dense
          rounded
          :class="props.msgIsError ? 'bg-red-1 text-red-9' : 'bg-blue-1 text-grey-9'"
        >
          {{ props.msg }}
        </q-banner>
      </div>
    </div>
  </div>
</template>
