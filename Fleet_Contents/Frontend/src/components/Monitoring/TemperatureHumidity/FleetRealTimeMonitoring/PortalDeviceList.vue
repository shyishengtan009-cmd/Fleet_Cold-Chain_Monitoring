<script setup lang="ts">
interface RegisteredDevice {
  hardware_id: string;
  label: string | null;
  registered_at: string | null;
}

const props = defineProps<{
  devices: RegisteredDevice[];
  devicesLoading: boolean;
  removingId: string | null;
}>();

const emit = defineEmits<{ (e: "remove", hw: string): void }>();
</script>

<template>
  <q-card-section class="q-pt-md q-px-lg q-pb-none">
    <div class="text-subtitle2 text-grey-8 q-mb-sm">
      <q-icon name="fa-solid fa-list" size="13px" class="q-mr-xs" />
      Registered Devices
    </div>

    <div v-if="props.devicesLoading" class="text-caption text-grey-6 q-mb-sm">
      <q-spinner size="14px" class="q-mr-xs" />
      Loading...
    </div>

    <div v-else-if="props.devices.length === 0" class="text-caption text-grey-5 q-mb-sm">
      No devices registered yet.
    </div>

    <q-list v-else dense bordered separator class="rounded-borders q-mb-md">
      <q-item v-for="d in props.devices" :key="d.hardware_id" class="q-py-xs">
        <q-item-section>
          <q-item-label class="text-body2 text-weight-medium">{{ d.hardware_id }}</q-item-label>
          <q-item-label caption>{{ d.label ?? "No label" }}</q-item-label>
        </q-item-section>
        <q-item-section side>
          <q-btn
            flat
            round
            dense
            icon="delete"
            color="negative"
            size="sm"
            :loading="props.removingId === d.hardware_id"
            @click="emit('remove', d.hardware_id)"
          >
            <q-tooltip>Remove device</q-tooltip>
          </q-btn>
        </q-item-section>
      </q-item>
    </q-list>
  </q-card-section>
</template>
