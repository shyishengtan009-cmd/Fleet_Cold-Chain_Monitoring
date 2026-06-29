<script setup lang="ts">
import { ref, watch } from "vue";
import api from "@/helpers/api";
import { STATUS_ALARM, STATUS_OK, STATUS_WARN } from "@/style/statusColors";

const props = defineProps<{ hardwareId: string | null }>();

type BreachRow = {
  field: string;
  alarmType: string;
  breachCount: number;
  avgValue: number | null;
  firstBreachTs: string | null;
  lastBreachTs: string | null;
};

const days = ref(30);
const breakdown = ref<BreachRow[]>([]);
const total = ref(0);
const loading = ref(false);

const DAYS_OPTIONS = [7, 30, 90];

const FIELD_LABEL: Record<string, string> = {
  temperature: "Temperature",
  humidity: "Humidity",
  battery: "Battery",
  light: "Light",
  dwell: "Dwell",
  geofence: "Geofence"
};

const FIELD_ICON: Record<string, string> = {
  temperature: "fa-solid fa-temperature-half",
  humidity:    "fa-solid fa-droplet",
  battery:     "fa-solid fa-battery-half",
  light:       "fa-solid fa-sun",
  dwell:       "fa-solid fa-clock",
  geofence:    "fa-solid fa-location-dot"
};

const columns = [
  { name: "field", label: "Sensor", field: "field", align: "left" as const },
  { name: "alarmType", label: "Level", field: "alarmType", align: "center" as const },
  {
    name: "breachCount",
    label: "Breaches",
    field: "breachCount",
    align: "center" as const,
    sortable: true
  },
  { name: "avgValue", label: "Avg Value", field: "avgValue", align: "center" as const },
  { name: "lastBreachTs", label: "Last Breach", field: "lastBreachTs", align: "left" as const }
];

async function fetchSummary() {
  if (!props.hardwareId) return;
  loading.value = true;
  try {
    const res = (await api.fleet.getBreachSummary(props.hardwareId, days.value)) as any;
    const d = res?.details ?? res;
    breakdown.value = d?.breakdown ?? [];
    total.value = d?.totalBreaches ?? 0;
  } catch {
    breakdown.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
}

watch(
  () => props.hardwareId,
  (hw) => {
    if (hw) fetchSummary();
  },
  { immediate: true }
);
watch(days, () => {
  if (props.hardwareId) fetchSummary();
});

function fmtTs(iso: string | null): string {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleString("en-MY", {
      timeZone: "Asia/Kuala_Lumpur",
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
      hour: "2-digit",
      minute: "2-digit",
      hour12: true
    });
  } catch {
    return iso;
  }
}

function fmtField(f: string): string {
  return FIELD_LABEL[f] ?? f;
}
</script>

<template>
  <div v-if="hardwareId">
    <div :class="[$style.sectionHeader, 'headerColor text-weight-bold q-pa-sm q-px-md']">
      Breach Analytics
    </div>

    <div class="q-pa-md">
      <div class="row items-center q-gutter-sm q-mb-md">
        <span class="text-caption text-grey-6">Lookback:</span>
        <q-btn-toggle
          v-model="days"
          :options="DAYS_OPTIONS.map((d) => ({ label: `${d}d`, value: d }))"
          no-caps
          dense
          unelevated
          toggle-color="primary"
          size="sm"
        />
        <q-space />
        <span v-if="!loading" class="text-caption text-grey-6">
          Total:
          <strong>{{ total }}</strong>
          breach{{ total !== 1 ? "es" : "" }}
        </span>
        <q-spinner v-if="loading" size="16px" color="primary" />
      </div>

      <!-- Breach table -->
      <q-table
        v-if="breakdown.length > 0"
        :rows="breakdown"
        :columns="columns"
        row-key="field"
        flat
        dense
        hide-pagination
        :rows-per-page-options="[0]"
        :class="$style.styledTable"
      >
        <template #body-cell-field="{ row }">
          <q-td>
            <div class="row items-center no-wrap gap-xs">
              <q-icon
                :name="FIELD_ICON[row.field] ?? 'fa-solid fa-circle-dot'"
                :color="row.alarmType === 'ALARM' ? 'negative' : 'warning'"
                size="13px"
                class="q-mr-xs"
              />
              {{ fmtField(row.field) }}
            </div>
          </q-td>
        </template>

        <template #body-cell-alarmType="{ row }">
          <q-td class="text-center">
            <q-chip
              dense
              square
              :color="row.alarmType === 'ALARM' ? 'red-1' : 'orange-1'"
              :text-color="row.alarmType === 'ALARM' ? 'negative' : 'warning'"
              style="font-size: 11px; font-weight: 600"
            >
              {{ row.alarmType }}
            </q-chip>
          </q-td>
        </template>

        <template #body-cell-breachCount="{ row }">
          <q-td class="text-center">
            <span
              class="text-weight-bold"
              :style="{ color: row.alarmType === 'ALARM' ? STATUS_ALARM : STATUS_WARN }"
            >
              {{ row.breachCount }}
            </span>
          </q-td>
        </template>

        <template #body-cell-avgValue="{ row }">
          <q-td class="text-center text-grey-7">
            {{ row.avgValue != null ? row.avgValue.toFixed(1) : "—" }}
          </q-td>
        </template>

        <template #body-cell-lastBreachTs="{ row }">
          <q-td class="text-caption text-grey-7">{{ fmtTs(row.lastBreachTs) }}</q-td>
        </template>
      </q-table>

      <!-- Empty state -->
      <div
        v-else-if="!loading"
        style="
          display: flex;
          flex-direction: column;
          align-items: center;
          justify-content: center;
          padding: 28px 16px;
          gap: 10px;
        "
      >
        <div
          style="
            width: 52px;
            height: 52px;
            border-radius: 50%;
            background: #e8f5e9;
            display: flex;
            align-items: center;
            justify-content: center;
          "
        >
          <q-icon name="fa-solid fa-shield-halved" color="positive" size="22px" />
        </div>
        <div class="text-weight-bold" :style="{ color: STATUS_OK }">All clear</div>
        <div class="text-caption text-grey-6" style="text-align: center; max-width: 260px">
          No temperature or condition breaches recorded in the last {{ days }} days.
        </div>
      </div>

      <!-- Loading skeleton -->
      <div v-if="loading" class="q-pa-md">
        <q-skeleton type="rect" height="12px" class="q-mb-sm" />
        <q-skeleton type="rect" height="12px" width="75%" class="q-mb-sm" />
        <q-skeleton type="rect" height="12px" width="50%" />
      </div>
    </div>
  </div>
</template>

<style lang="sass" module>
@import '../../../../style/_variables'

.sectionHeader
  width: 100%
  font-size: 14px
  text-transform: uppercase
  color: $primary-black
  border-top: 1px solid $secondary-grey-2

.styledTable
  :global(thead tr th)
    font-weight: 700 !important
    font-size: 14px !important
    color: $primary-black !important
    text-transform: capitalize !important
    background-image: linear-gradient(0deg, $secondary-grey-2 0%, $white 100%) !important
    box-sizing: border-box !important
    border-top: 1px solid $secondary-grey-2 !important
  :global(tbody tr td)
    padding: 8px !important
    font-size: 14px !important
    color: $primary-black !important
</style>
