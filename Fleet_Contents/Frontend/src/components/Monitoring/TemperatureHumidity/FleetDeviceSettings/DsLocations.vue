<script setup lang="ts">
import { ref, onMounted, watch } from "vue";
import api from "@/helpers/api";
import LabelApp from "@/components/common/LabelApp.vue";

type LocationRow = {
  id: number;
  name: string;
  lat: number;
  lng: number;
  radius_m: number;
  max_dwell_min: number | null;
  type: string;
};

const typeOptions = [
  { label: "Depot / Base",   value: "depot"     },
  { label: "Refuel Stop",    value: "refuel"    },
  { label: "Rest Stop",      value: "rest"      },
  { label: "Customer Site",  value: "customer"  },
  { label: "Other",          value: "other"     },
  { label: "Forbidden Zone", value: "forbidden" }
];

const typeBadgeStyle = (type: string): string => {
  const colors: Record<string, string> = {
    depot:     "#546e7a",
    refuel:    "#e65100",
    rest:      "#1565c0",
    customer:  "#2e7d32",
    other:     "#6a1b9a",
    forbidden: "#b71c1c"
  };
  return `background:${colors[type] ?? "#757575"};color:#fff`;
};

const typeLabel = (type: string) => typeOptions.find((o) => o.value === type)?.label ?? type;

const locations = ref<LocationRow[]>([]);
const loading   = ref(false);
const saving    = ref(false);
const deleting  = ref<number | null>(null);
const msg       = ref("");
const msgError  = ref(false);
const showDialog = ref(false);
const isEdit     = ref(false);

const emptyForm = (): LocationRow => ({
  id: 0, name: "", lat: 0, lng: 0, radius_m: 200, max_dwell_min: null, type: "other"
});
const form = ref<LocationRow>(emptyForm());

function openAdd() {
  form.value = emptyForm();
  isEdit.value = false;
  showDialog.value = true;
}

function openEdit(row: LocationRow) {
  form.value = { ...row };
  isEdit.value = true;
  showDialog.value = true;
}

async function load() {
  loading.value = true;
  msg.value = "";
  try {
    const res = (await api.fleet.getLocations()) as any;
    locations.value = res?.details?.locations ?? res?.data?.locations ?? [];
  } catch (e: unknown) {
    msg.value = e instanceof Error ? e.message : String(e);
    msgError.value = true;
  } finally {
    loading.value = false;
  }
}

async function save() {
  if (!form.value.name.trim()) {
    msg.value = "Name is required.";
    msgError.value = true;
    return;
  }
  saving.value = true;
  msg.value = "";
  try {
    await api.fleet.saveLocation({
      id:            form.value.id,
      name:          form.value.name.trim(),
      lat:           Number(form.value.lat),
      lng:           Number(form.value.lng),
      radius_m:      Number(form.value.radius_m),
      max_dwell_min: form.value.type === "forbidden" ? null
                     : (form.value.max_dwell_min != null ? Number(form.value.max_dwell_min) : null),
      type:          form.value.type
    });
    showDialog.value = false;
    await load();
    msg.value = isEdit.value ? "Location updated." : "Location added.";
    msgError.value = false;
  } catch (e: unknown) {
    msg.value = e instanceof Error ? e.message : String(e);
    msgError.value = true;
  } finally {
    saving.value = false;
  }
}

async function remove(id: number) {
  deleting.value = id;
  try {
    await api.fleet.deleteLocation(id);
    await load();
  } catch (e: unknown) {
    msg.value = e instanceof Error ? e.message : String(e);
    msgError.value = true;
  } finally {
    deleting.value = null;
  }
}

onMounted(load);
</script>

<template>
  <div class="q-pa-md">
    <q-banner
      v-if="msg"
      :class="msgError ? 'bg-red-1 text-negative' : 'bg-green-1 text-positive'"
      dense
      class="q-mb-sm rounded-borders"
    >
      {{ msg }}
    </q-banner>

    <div class="row justify-between items-center q-mb-sm">
      <div class="text-caption text-grey-6" style="max-width:520px">
        Locations are shared across all devices in your organisation. Set type to
        <strong>Forbidden Zone</strong> to trigger an immediate ALARM when any device enters the area.
      </div>
      <q-btn size="sm" no-caps unelevated color="primary" icon="add" label="Add Location" @click="openAdd" />
    </div>

    <q-table
      :rows="locations"
      :columns="[
        { name: 'name',     label: 'Name',        field: 'name',          align: 'left'  as const },
        { name: 'type',     label: 'Type',        field: 'type',          align: 'left'  as const },
        { name: 'radius',   label: 'Radius',      field: 'radius_m',      align: 'right' as const },
        { name: 'dwell',    label: 'Dwell Limit', field: 'max_dwell_min', align: 'right' as const },
        { name: 'coords',   label: 'Coordinates', field: 'lat',           align: 'left'  as const },
        { name: 'actions',  label: '',            field: 'id',            align: 'right' as const }
      ]"
      row-key="id"
      flat
      dense
      :loading="loading"
      hide-bottom
      :class="$style.styledTable"
    >
      <template #body-cell-type="slotProps">
        <q-td :props="slotProps">
          <span
            class="text-caption text-weight-bold q-px-xs q-py-none rounded-borders"
            :style="typeBadgeStyle(slotProps.row.type)"
          >
            {{ typeLabel(slotProps.row.type) }}
          </span>
        </q-td>
      </template>

      <template #body-cell-radius="slotProps">
        <q-td :props="slotProps" class="text-right">{{ slotProps.row.radius_m }} m</q-td>
      </template>

      <template #body-cell-dwell="slotProps">
        <q-td :props="slotProps" class="text-right">
          <span v-if="slotProps.row.type === 'forbidden'" class="text-grey-4">—</span>
          <span v-else-if="slotProps.row.max_dwell_min == null" class="text-grey-5">No limit</span>
          <span v-else>{{ slotProps.row.max_dwell_min }} min</span>
        </q-td>
      </template>

      <template #body-cell-coords="slotProps">
        <q-td :props="slotProps" class="text-caption text-grey-5">
          {{ Number(slotProps.row.lat).toFixed(5) }}, {{ Number(slotProps.row.lng).toFixed(5) }}
        </q-td>
      </template>

      <template #body-cell-actions="slotProps">
        <q-td :props="slotProps" class="text-right">
          <q-btn flat dense round size="sm" icon="edit"   color="primary"  @click="openEdit(slotProps.row)" />
          <q-btn flat dense round size="sm" icon="delete" color="negative"
            :loading="deleting === slotProps.row.id"
            @click="remove(slotProps.row.id)"
          />
        </q-td>
      </template>

      <template #no-data>
        <div class="text-center q-pa-md text-grey-5 text-caption">No locations added yet.</div>
      </template>
    </q-table>
  </div>

  <!-- Add / Edit dialog -->
  <q-dialog v-model="showDialog" persistent>
    <q-card style="min-width:420px;max-width:520px">
      <q-card-section class="text-weight-bold">
        {{ isEdit ? "Edit Location" : "Add Location" }}
      </q-card-section>
      <q-separator />
      <q-card-section>
        <div class="row q-col-gutter-sm">
          <LabelApp label="Name" class="col-12">
            <q-input dense outlined v-model="form.name" placeholder="e.g. Restricted Warehouse A" />
          </LabelApp>

          <LabelApp label="Type" class="col-12 col-sm-6">
            <q-select dense outlined emit-value map-options
              :options="typeOptions"
              v-model="form.type"
            />
          </LabelApp>

          <LabelApp label="Radius (m)" tooltip="50 – 10 000 m" class="col-12 col-sm-6">
            <q-input dense outlined type="number" min="50" max="10000"
              v-model.number="form.radius_m"
            />
          </LabelApp>

          <LabelApp label="Latitude" tooltip="e.g. 3.14159 — find via Google Maps" class="col-12 col-sm-6">
            <q-input dense outlined type="number" step="0.00001" v-model.number="form.lat" />
          </LabelApp>

          <LabelApp label="Longitude" tooltip="e.g. 101.68653" class="col-12 col-sm-6">
            <q-input dense outlined type="number" step="0.00001" v-model.number="form.lng" />
          </LabelApp>

          <LabelApp
            v-if="form.type !== 'forbidden'"
            label="Dwell Limit (min)"
            tooltip="Blank = no dwell alert at this stop"
            class="col-12 col-sm-6"
          >
            <q-input dense outlined type="number" min="1"
              v-model.number="form.max_dwell_min"
            />
          </LabelApp>

          <div v-if="form.type === 'forbidden'" class="col-12">
            <q-banner class="bg-red-1 text-negative rounded-borders" dense>
              Forbidden zones fire an ALARM the moment any device enters the area.
              Dwell limit does not apply.
            </q-banner>
          </div>
        </div>
      </q-card-section>
      <q-card-actions align="right">
        <q-btn flat no-caps label="Cancel" v-close-popup />
        <q-btn unelevated no-caps color="primary" label="Save" :loading="saving" @click="save" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<style lang="sass" module>
@import '../../../../style/_variables'

.styledTable
  :global(thead tr th)
    font-weight: 700 !important
    font-size: 11px !important
    letter-spacing: 0.3px !important
    color: #515151 !important
    text-transform: uppercase !important
    background-image: linear-gradient(0deg, $secondary-grey-2 0%, $white 100%) !important
    box-sizing: border-box !important
    padding: 7px 10px !important
    border-bottom: 2px solid $secondary-grey-2 !important
  :global(tbody tr td)
    padding: 7px 10px !important
    font-size: 12px !important
    color: #515151 !important
</style>
