<script setup lang="ts">
import { Line } from "vue-chartjs";

const props = defineProps<{
  historyLoading: boolean;
  historyRows: unknown[];
  chartTempHum: any;
  chartTempHumOpts: any;
  chartLight: any;
  chartLightOpts: any;
  chartBattery: any;
  chartBatteryOpts: any;
  hasVibration: boolean;
  chartVibration: any;
  chartVibrationOpts: any;
  hasRssi: boolean;
  chartRssi: any;
  chartRssiOpts: any;
}>();
</script>

<template>
  <div>
    <div v-if="historyLoading" class="text-center q-pa-lg">
      <q-spinner color="primary" size="32px" />
    </div>

    <div v-else-if="historyRows.length" class="column">
      <!-- Temperature & Humidity -->
      <q-card flat>
        <q-card-section class="headerColor text-weight-bold" style="font-size: 12px; letter-spacing: 0.4px; text-transform: uppercase; color: #515151">
          Temperature &amp; Humidity
        </q-card-section>
        <q-separator />
        <q-card-section>
          <div :class="$style.chartWrapLg">
            <Line :data="chartTempHum" :options="chartTempHumOpts" />
          </div>
        </q-card-section>
      </q-card>

      <!-- Light -->
      <q-card flat>
        <q-card-section class="headerColor text-weight-bold" style="font-size: 12px; letter-spacing: 0.4px; text-transform: uppercase; color: #515151">
          Light
        </q-card-section>
        <q-separator />
        <q-card-section>
          <div :class="$style.chartWrapLg">
            <Line :data="chartLight" :options="chartLightOpts" />
          </div>
        </q-card-section>
      </q-card>

      <!-- Battery -->
      <q-card flat>
        <q-card-section class="headerColor text-weight-bold" style="font-size: 12px; letter-spacing: 0.4px; text-transform: uppercase; color: #515151">
          Battery
        </q-card-section>
        <q-separator />
        <q-card-section>
          <div :class="$style.chartWrapLg">
            <Line :data="chartBattery" :options="chartBatteryOpts" />
          </div>
        </q-card-section>
      </q-card>

      <!-- Vibration -->
      <q-card flat>
        <q-card-section class="headerColor text-weight-bold" style="font-size: 12px; letter-spacing: 0.4px; text-transform: uppercase; color: #515151">
          Vibration
        </q-card-section>
        <q-separator />
        <q-card-section>
          <div :class="$style.chartWrapLg">
            <Line v-if="hasVibration" :data="chartVibration" :options="chartVibrationOpts" />
            <div v-else class="text-center q-pa-lg text-grey-5">
              No vibration data in raw
            </div>
          </div>
        </q-card-section>
      </q-card>

      <!-- RSSI -->
      <q-card flat>
        <q-card-section class="headerColor text-weight-bold" style="font-size: 12px; letter-spacing: 0.4px; text-transform: uppercase; color: #515151">
          RSSI
        </q-card-section>
        <q-separator />
        <q-card-section>
          <div :class="$style.chartWrapLg">
            <Line v-if="hasRssi" :data="chartRssi" :options="chartRssiOpts" />
            <div v-else class="text-center q-pa-lg text-grey-5">No RSSI data in raw</div>
          </div>
        </q-card-section>
      </q-card>
    </div>

    <div v-else class="text-center q-pa-lg text-grey-5">No history data</div>
  </div>
</template>

<style lang="sass" module>
@import '../../../../style/_variables'

.chartWrapLg
  position: relative
  height: 300px
  cursor: crosshair
</style>
