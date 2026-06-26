<script setup lang="ts">
import { ref } from "vue";
import type { TripOption } from "./useFleetExport";
import api from "@/helpers/api";

const props = defineProps<{
  enabled: boolean;
  tripOptions: TripOption[];
  tripsLoading: boolean;
}>();

const emit = defineEmits<{
  (e: "update:enabled", v: boolean): void;
}>();

function selectAll(val: boolean) {
  props.tripOptions.forEach((t) => (t.selected = val));
}

const downloadingId = ref<number | null>(null);

async function downloadPdf(trip: TripOption) {
  downloadingId.value = trip.tripId;
  try {
    const blob = (await api.fleet.downloadTripReport(trip.tripId)) as Blob;
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `TripReport_${trip.tripId}.pdf`;
    a.click();
    URL.revokeObjectURL(url);
  } catch {
    // silent — toast handled by global error interceptor
  } finally {
    downloadingId.value = null;
  }
}
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
        <div class="text-subtitle2 text-weight-bold">Trip Coordinates</div>
        <div class="text-caption text-grey-6">
          GPS points (lat/lng/timestamp) for selected trips
        </div>
      </div>
    </div>

    <template v-if="props.enabled">
      <q-inner-loading :showing="props.tripsLoading" />

      <div
        v-if="!props.tripsLoading && props.tripOptions.length === 0"
        class="text-caption text-grey-5 q-ml-md q-mb-sm"
      >
        No trips recorded for this device.
      </div>

      <template v-if="!props.tripsLoading && props.tripOptions.length > 0">
        <div class="row items-center q-ml-md q-mb-xs">
          <div class="text-caption text-grey-7">
            {{ props.tripOptions.filter((t) => t.selected).length }} /
            {{ props.tripOptions.length }} trips selected
          </div>
          <q-space />
          <q-btn flat dense no-caps size="sm" label="Select All" @click="selectAll(true)" />
          <q-btn flat dense no-caps size="sm" label="Deselect All" @click="selectAll(false)" />
        </div>
        <div class="trip-list q-ml-md">
          <div
            v-for="trip in props.tripOptions"
            :key="trip.tripId"
            class="row items-center trip-row"
          >
            <q-checkbox v-model="trip.selected" dense color="primary" />
            <div class="text-caption text-grey-8 q-ml-xs ellipsis" style="max-width: 300px">
              {{ trip.label }}
            </div>
            <q-space />
            <q-btn
              flat
              dense
              no-caps
              size="xs"
              icon="fa-solid fa-file-pdf"
              color="red-7"
              :loading="downloadingId === trip.tripId"
              title="Download PDF report"
              class="q-mr-xs"
              @click.stop="downloadPdf(trip)"
            />
          </div>
        </div>
      </template>
    </template>
  </div>
</template>

<style lang="sass" scoped>
@import '../../../../style/_variables'

.export-section
  transition: opacity 0.2s

.export-section--disabled
  opacity: 0.45
  pointer-events: none

.trip-list
  max-height: 180px
  overflow-y: auto
  border: 1px solid $secondary-grey-2
  border-radius: 6px
  padding: 4px 0

.trip-row
  padding: 2px 8px

.trip-row:hover
  background: $primary-grey-1
</style>
