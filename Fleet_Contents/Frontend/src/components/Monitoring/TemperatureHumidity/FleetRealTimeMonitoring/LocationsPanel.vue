<script setup lang="ts">
import { ref, onMounted } from "vue";
import api from "@/helpers/api";

export type FleetLocation = {
  id: number;
  name: string;
  lat: number;
  lng: number;
  radius_m: number;
  max_dwell_min: number | null;
  type: string;
};

const emit = defineEmits<{
  (e: "update", locations: FleetLocation[]): void;
  (e: "pick-on-map"): void; // tell parent to enter map-click mode
}>();

const props = defineProps<{
  pendingLat?: number | null;
  pendingLng?: number | null;
}>();

const locations = ref<FleetLocation[]>([]);
const loading = ref(false);
const loadError = ref<string | null>(null);
const showDialog = ref(false);
const saving = ref(false);
const deleting = ref<number | null>(null);

const typeOptions = [
  { label: "Depot / Base", value: "depot" },
  { label: "Refuel Stop", value: "refuel" },
  { label: "Rest Stop", value: "rest" },
  { label: "Customer Site", value: "customer" },
  { label: "Other", value: "other" },
  { label: "Forbidden Zone", value: "forbidden" }
];

const typeLabel = (t: string) => typeOptions.find((o) => o.value === t)?.label ?? t;
const typeColor = (t: string) =>
  ({
    depot: "grey-7",
    refuel: "orange-7",
    rest: "blue-5",
    customer: "green-7",
    other: "purple-5",
    forbidden: "red-9"
  })[t] ?? "grey-5";

const emptyForm = (): Partial<FleetLocation> => ({
  id: 0,
  name: "",
  lat: 0,
  lng: 0,
  radius_m: 200,
  max_dwell_min: null,
  type: "other"
});
const form = ref<Partial<FleetLocation>>(emptyForm());
const isEdit = ref(false);

async function load() {
  loading.value = true;
  loadError.value = null;
  try {
    const res = (await api.fleet.getLocations()) as any;
    locations.value = res?.details?.locations ?? [];
    emit("update", locations.value);
  } catch (e: unknown) {
    locations.value = [];
    loadError.value = e instanceof Error ? e.message : "Failed to load locations";
  } finally {
    loading.value = false;
  }
}

function openAdd() {
  form.value = emptyForm();
  isEdit.value = false;
  showDialog.value = true;
}

function openEdit(loc: FleetLocation) {
  form.value = { ...loc };
  isEdit.value = true;
  showDialog.value = true;
}

function pickOnMap() {
  emit("pick-on-map");
}

// Called by parent when user clicks on the map in pick mode
function applyMapClick(lat: number, lng: number) {
  form.value = { ...form.value, lat: parseFloat(lat.toFixed(6)), lng: parseFloat(lng.toFixed(6)) };
  showDialog.value = true;
}

async function save() {
  if (!form.value.name?.trim()) return;
  saving.value = true;
  try {
    await api.fleet.saveLocation({
      id: form.value.id ?? 0,
      name: form.value.name.trim(),
      lat: Number(form.value.lat),
      lng: Number(form.value.lng),
      radius_m: Number(form.value.radius_m) || 200,
      max_dwell_min: form.value.max_dwell_min != null ? Number(form.value.max_dwell_min) : null,
      type: form.value.type ?? "other"
    });
    showDialog.value = false;
    await load();
  } finally {
    saving.value = false;
  }
}

async function remove(loc: FleetLocation) {
  deleting.value = loc.id;
  try {
    await api.fleet.deleteLocation(loc.id);
    await load();
  } finally {
    deleting.value = null;
  }
}

onMounted(load);

defineExpose({ applyMapClick, load, locations });
</script>

<template>
  <div>
    <!-- Header + Add button -->
    <div class="row items-center q-px-md q-pt-sm q-pb-xs">
      <q-icon name="fa-solid fa-map-pin" size="13px" color="primary" class="q-mr-xs" />
      <span class="text-caption text-weight-bold" style="font-size: 12px">Named Locations</span>
      <q-space />
      <q-btn
        flat
        dense
        no-caps
        size="sm"
        color="primary"
        icon="fa-solid fa-plus"
        label="Add"
        @click="openAdd"
      />
    </div>

    <q-separator />

    <!-- Load error -->
    <div
      v-if="loadError"
      class="q-px-md q-py-xs text-negative"
      style="font-size: 11px; display: flex; align-items: center; gap: 4px"
    >
      <q-icon name="fa-solid fa-circle-exclamation" size="11px" />
      {{ loadError }}
      <q-btn
        flat
        dense
        no-caps
        size="xs"
        label="Retry"
        color="negative"
        class="q-ml-xs"
        @click="load"
      />
    </div>

    <!-- Location list -->
    <q-scroll-area style="height: 220px">
      <q-list dense>
        <q-item v-if="loading">
          <q-item-section class="text-grey text-caption text-center">Loading…</q-item-section>
        </q-item>
        <q-item v-else-if="!locations.length">
          <q-item-section class="text-grey text-caption text-center q-py-sm">
            No locations saved. Add one to set custom dwell limits.
          </q-item-section>
        </q-item>
        <q-item v-for="loc in locations" :key="loc.id" class="q-py-xs">
          <q-item-section avatar style="min-width: 28px">
            <q-icon name="fa-solid fa-circle" size="8px" :color="typeColor(loc.type)" />
          </q-item-section>
          <q-item-section>
            <q-item-label style="font-size: 12px; font-weight: 600">{{ loc.name }}</q-item-label>
            <q-item-label caption style="font-size: 10px">
              {{ typeLabel(loc.type) }} · r={{ loc.radius_m }}m ·
              <span v-if="loc.max_dwell_min != null">max {{ loc.max_dwell_min }} min</span>
              <span v-else class="text-grey-4">no limit</span>
            </q-item-label>
          </q-item-section>
          <q-item-section side>
            <div class="row items-center" style="gap: 2px">
              <q-btn
                flat
                dense
                round
                size="xs"
                icon="fa-solid fa-pen"
                color="grey-7"
                @click="openEdit(loc)"
              />
              <q-btn
                flat
                dense
                round
                size="xs"
                icon="fa-solid fa-trash"
                color="negative"
                :loading="deleting === loc.id"
                @click="remove(loc)"
              />
            </div>
          </q-item-section>
        </q-item>
      </q-list>
    </q-scroll-area>

    <!-- Add / Edit dialog -->
    <q-dialog v-model="showDialog" persistent>
      <q-card style="min-width: 340px">
        <q-card-section class="q-pb-xs">
          <div class="text-subtitle2">{{ isEdit ? "Edit Location" : "Add Location" }}</div>
        </q-card-section>

        <q-card-section class="q-pt-xs q-gutter-sm">
          <q-input v-model="form.name" dense outlined label="Name *" />
          <div class="row q-col-gutter-sm">
            <q-input
              v-model.number="form.lat"
              dense
              outlined
              label="Latitude"
              class="col"
              type="number"
              step="0.000001"
            />
            <q-input
              v-model.number="form.lng"
              dense
              outlined
              label="Longitude"
              class="col"
              type="number"
              step="0.000001"
            />
          </div>
          <q-btn
            flat
            dense
            no-caps
            size="sm"
            color="primary"
            icon="fa-solid fa-crosshairs"
            label="Pick on Map"
            @click="pickOnMap"
          />
          <q-select
            v-model="form.type"
            dense
            outlined
            label="Type"
            :options="typeOptions"
            option-label="label"
            option-value="value"
            emit-value
            map-options
          />
          <q-input
            v-model.number="form.radius_m"
            dense
            outlined
            label="Radius (m)"
            type="number"
            min="50"
            max="10000"
          />
          <q-input
            v-model.number="form.max_dwell_min"
            dense
            outlined
            label="Max Dwell (min) — blank = no alert here"
            type="number"
            min="1"
            clearable
          />
        </q-card-section>

        <q-card-actions align="right">
          <q-btn flat no-caps label="Cancel" @click="showDialog = false" />
          <q-btn no-caps color="primary" label="Save" :loading="saving" @click="save" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>
