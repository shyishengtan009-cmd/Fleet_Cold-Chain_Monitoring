<script setup lang="ts">
import HeaderAppV2 from "@/components/common/HeaderAppV2.vue";

const emit = defineEmits<{ (e: "add-device"): void }>();
</script>

<template>
  <HeaderAppV2
    titlePage="Device Settings"
    :breadcrumbs="['Monitoring', 'Fleet', 'Device Settings']"
  >
    <div class="row items-center gap-sm q-mt-xs">
      <q-btn
        no-caps
        color="primary"
        icon="fa-solid fa-plus"
        label="Add Device"
        @click="emit('add-device')"
      />
    </div>
  </HeaderAppV2>
</template>
