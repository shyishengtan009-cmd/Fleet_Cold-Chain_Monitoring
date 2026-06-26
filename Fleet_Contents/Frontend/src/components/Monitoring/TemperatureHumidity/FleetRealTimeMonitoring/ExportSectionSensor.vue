<script setup lang="ts">
const props = defineProps<{
  enabled: boolean;
  start: string;
  end: string;
}>();

const emit = defineEmits<{
  (e: "update:enabled", v: boolean): void;
  (e: "update:start", v: string): void;
  (e: "update:end", v: string): void;
}>();
</script>

<template>
  <div class="export-section" :class="{ 'export-section--disabled': !props.enabled }">
    <div class="row items-center q-mb-sm">
      <q-checkbox
        :model-value="props.enabled"
        color="primary"
        @update:model-value="(v) => emit('update:enabled', !!v)"
      />
      <div class="q-ml-xs">
        <div class="text-subtitle2 text-weight-bold">Sensor Data</div>
        <div class="text-caption text-grey-6">
          Temperature, Humidity, Light, Battery, Vibration, RSSI
        </div>
      </div>
    </div>
    <div v-if="props.enabled" class="row q-col-gutter-sm q-ml-md">
      <div class="col-6">
        <q-input
          :model-value="props.start"
          type="datetime-local"
          label="From"
          outlined
          dense
          @update:model-value="(v) => emit('update:start', String(v ?? ''))"
        />
      </div>
      <div class="col-6">
        <q-input
          :model-value="props.end"
          type="datetime-local"
          label="To"
          outlined
          dense
          @update:model-value="(v) => emit('update:end', String(v ?? ''))"
        />
      </div>
      <div class="col-12">
        <div class="text-caption text-grey-5">Up to 5,000 rows per export. Times are UTC.</div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.export-section {
  transition: opacity 0.2s;
}
.export-section--disabled {
  opacity: 0.45;
  pointer-events: none;
}
</style>
