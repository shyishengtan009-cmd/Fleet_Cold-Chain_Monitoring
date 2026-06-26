<script setup lang="ts">
import { useFleetExport } from "./useFleetExport";
import ExportSectionSensor from "./ExportSectionSensor.vue";
import ExportSectionTrips from "./ExportSectionTrips.vue";
import ExportSectionShipment from "./ExportSectionShipment.vue";
import ExportSectionAlarms from "./ExportSectionAlarms.vue";

const props = defineProps<{ hardwareId: string }>();
const emit = defineEmits<{ (e: "close"): void }>();

const {
  exportSensor,
  sensorStart,
  sensorEnd,
  exportTrips,
  tripOptions,
  tripsLoading,
  anyTripSelected,
  exportShipment,
  exportAlarms,
  alarmStart,
  alarmEnd,
  exporting,
  error,
  step,
  runExport
} = useFleetExport(() => props.hardwareId);
</script>

<template>
  <q-dialog :model-value="true" persistent @hide="emit('close')">
    <q-card
      style="
        width: 560px;
        max-width: 95vw;
        max-height: 90vh;
        border-radius: 12px;
        display: flex;
        flex-direction: column;
        overflow: hidden;
      "
    >
      <div
        style="
          background: linear-gradient(135deg, #1976d2, #0d47a1);
          padding: 20px 24px;
          display: flex;
          align-items: center;
          gap: 14px;
        "
      >
        <q-icon name="fa-solid fa-file-excel" size="28px" color="white" />
        <div>
          <div class="text-h6 text-white text-weight-bold">Export to Excel</div>
          <div class="text-caption text-white" style="opacity: 0.85">Device: {{ hardwareId }}</div>
        </div>
        <q-space />
        <q-btn flat round dense icon="close" color="white" @click="emit('close')" />
      </div>

      <q-card-section v-if="step === 'done'" class="text-center q-py-xl">
        <q-icon name="fa-solid fa-circle-check" color="positive" size="52px" class="q-mb-md" />
        <div class="text-h6 q-mb-xs">Export Complete</div>
        <div class="text-grey-7 q-mb-lg">Your Excel file has been downloaded.</div>
        <q-btn
          label="Export Another"
          outline
          color="primary"
          no-caps
          class="q-mr-sm"
          @click="step = 'config'"
        />
        <q-btn label="Close" unelevated color="primary" no-caps @click="emit('close')" />
      </q-card-section>

      <template v-else>
        <q-card-section class="q-px-lg q-pt-lg q-pb-none" style="overflow-y: auto; flex: 1 1 auto">
          <ExportSectionSensor
            v-model:enabled="exportSensor"
            v-model:start="sensorStart"
            v-model:end="sensorEnd"
          />
          <q-separator class="q-my-md" />
          <ExportSectionTrips
            v-model:enabled="exportTrips"
            :trip-options="tripOptions"
            :trips-loading="tripsLoading"
          />
          <q-separator class="q-my-md" />
          <ExportSectionShipment v-model:enabled="exportShipment" />
          <q-separator class="q-my-md" />
          <ExportSectionAlarms
            v-model:enabled="exportAlarms"
            v-model:start="alarmStart"
            v-model:end="alarmEnd"
          />
        </q-card-section>

        <q-card-section v-if="error" class="q-px-lg q-pt-sm q-pb-none" style="flex-shrink: 0">
          <q-banner class="bg-red-1 text-red-9 rounded-borders" dense>
            <template #avatar>
              <q-icon name="fa-solid fa-circle-exclamation" color="negative" />
            </template>
            {{ error }}
          </q-banner>
        </q-card-section>

        <q-card-actions
          class="q-px-lg q-py-md"
          :class="$style.footerActions"
          align="right"
          style="flex-shrink: 0"
        >
          <q-btn flat no-caps label="Cancel" color="grey-7" @click="emit('close')" />
          <q-btn
            unelevated
            no-caps
            color="primary"
            icon="fa-solid fa-download"
            label="Export"
            :loading="exporting"
            :disable="
              (!exportSensor && !exportTrips && !exportShipment && !exportAlarms) ||
              (exportTrips && !anyTripSelected && tripOptions.length > 0)
            "
            @click="runExport"
          />
        </q-card-actions>
      </template>
    </q-card>
  </q-dialog>
</template>

<style lang="sass" module>
@import '../../../../style/_variables'

.footerActions
  border-top: 1px solid $secondary-grey-2
</style>
