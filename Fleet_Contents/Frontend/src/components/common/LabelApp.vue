<script setup lang="ts">
const props = defineProps({
  label: {
    type: String,
    default: ""
  },
  required: {
    type: Boolean,
    default: false
  },
  tooltip: {
    type: String,
    default: ""
  }
});
</script>

<template>
  <div class="column input-column">
    <label class="label-with-icon">
      {{ props.label }}
      <b class="text-negative" v-if="props.required">*</b>

      <q-icon
        v-if="props.tooltip"
        name="fa-solid fa-info-circle"
        color="grey"
        class="q-ml-xs"
        style="cursor: pointer; font-size: 12px"
      >
        <q-tooltip
          :class="[{ hidden: !$q.screen.gt.xs }]"
          anchor="center right"
          self="center left"
          max-width="280px"
        >
          {{ props.tooltip }}
        </q-tooltip>
      </q-icon>
    </label>
    <slot></slot>
  </div>
</template>

<style scoped>
.label-with-icon {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}
</style>
