<script setup lang="ts">
const props = defineProps<{ enabled: boolean }>();
const emit = defineEmits<{ (e: "update:enabled", v: boolean): void }>();
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
        <div class="text-subtitle2 text-weight-bold">Shipment &amp; Device Settings</div>
        <div class="text-caption text-grey-6">
          Shipment info, alarm thresholds (current settings)
        </div>
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
