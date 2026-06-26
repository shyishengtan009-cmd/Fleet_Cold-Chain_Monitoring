<script lang="ts">
import { asObject, toBool, toNum, toStr } from "./dsUtils";

export type TripSettings = {
  shipment_id: string;
  goods: string;
  carrier: string;
  sender: string;
  truck_name: string;
  estimated_time: string;
  remark: string;
  customer_company: string;
  asset_id: string;
  transport_route: string;
  receiver: string;
  notify_email: string;
  trip_min_minutes: number | null;
  trip_max_minutes: number | null;
  geofence_required: boolean;
  start_location: string;
  end_location: string;
  start_lat: number | null;
  start_lng: number | null;
  end_lat: number | null;
  end_lng: number | null;
};

export const tripDefaults: TripSettings = {
  shipment_id: "",
  goods: "",
  carrier: "",
  sender: "",
  truck_name: "",
  estimated_time: "",
  remark: "",
  customer_company: "",
  asset_id: "",
  transport_route: "",
  receiver: "",
  notify_email: "",
  trip_min_minutes: null,
  trip_max_minutes: null,
  geofence_required: false,
  start_location: "",
  end_location: "",
  start_lat: null,
  start_lng: null,
  end_lat: null,
  end_lng: null
};

export function normalizeTrip(obj: unknown): TripSettings {
  const t = asObject(obj);
  return {
    ...tripDefaults,
    shipment_id: toStr(t.shipment_id),
    goods: toStr(t.goods),
    carrier: toStr(t.carrier),
    sender: toStr(t.sender),
    truck_name: toStr(t.truck_name ?? t.vehicle),
    estimated_time: toStr(t.estimated_time),
    remark: toStr(t.remark),
    customer_company: toStr(t.customer_company),
    asset_id: toStr(t.asset_id),
    transport_route: toStr(t.transport_route),
    receiver: toStr(t.receiver),
    trip_min_minutes: toNum(t.trip_min_minutes),
    trip_max_minutes: toNum(t.trip_max_minutes),
    geofence_required: toBool(t.geofence_required),
    start_location: toStr(t.start_location),
    end_location: toStr(t.end_location),
    start_lat: toNum(t.start_lat),
    start_lng: toNum(t.start_lng),
    end_lat: toNum(t.end_lat),
    end_lng: toNum(t.end_lng),
    notify_email: toStr(t.notify_email)
  };
}

export function serializeTrip(t: TripSettings) {
  return {
    shipment_id: String(t.shipment_id || ""),
    goods: String(t.goods || ""),
    carrier: String(t.carrier || ""),
    sender: String(t.sender || ""),
    truck_name: String(t.truck_name || ""),
    estimated_time: String(t.estimated_time || ""),
    remark: String(t.remark || ""),
    customer_company: String(t.customer_company || ""),
    asset_id: String(t.asset_id || ""),
    transport_route: String(t.transport_route || ""),
    receiver: String(t.receiver || ""),
    trip_min_minutes: t.trip_min_minutes == null ? null : Number(t.trip_min_minutes),
    trip_max_minutes: t.trip_max_minutes == null ? null : Number(t.trip_max_minutes),
    geofence_required: Boolean(t.geofence_required),
    start_location: String(t.start_location || ""),
    end_location: String(t.end_location || ""),
    start_lat: t.start_lat == null ? null : Number(t.start_lat),
    start_lng: t.start_lng == null ? null : Number(t.start_lng),
    end_lat: t.end_lat == null ? null : Number(t.end_lat),
    end_lng: t.end_lng == null ? null : Number(t.end_lng),
    notify_email: String(t.notify_email || "")
  };
}
</script>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from "vue";
import api from "@/helpers/api";
import LabelApp from "@/components/common/LabelApp.vue";

type LocationOption = {
  id: number;
  name: string;
  lat: number;
  lng: number;
  type: string;
  source?: "saved" | "geo";
};

const savedLocations = ref<LocationOption[]>([]);
const startOptions = ref<LocationOption[]>([]);
const endOptions = ref<LocationOption[]>([]);
const hourSelectRef = ref<{ hidePopup: () => void } | null>(null);
const minuteSelectRef = ref<{ hidePopup: () => void } | null>(null);
function onTimeSelectScroll() { hourSelectRef.value?.hidePopup(); minuteSelectRef.value?.hidePopup(); }
function onTimeSelectShow() { window.addEventListener("scroll", onTimeSelectScroll, true); }
function onTimeSelectHide() { window.removeEventListener("scroll", onTimeSelectScroll, true); }

onMounted(async () => {
  try {
    const res = (await api.fleet.getLocations()) as any;
    savedLocations.value = (res?.details?.locations ?? []).map((l: any) => ({
      ...l,
      source: "saved" as const
    }));
    startOptions.value = [...savedLocations.value];
    endOptions.value = [...savedLocations.value];
  } catch {
    savedLocations.value = [];
  }
});

onUnmounted(() => {
  if (startTimer.value) clearTimeout(startTimer.value);
  if (endTimer.value) clearTimeout(endTimer.value);
});

async function geocodePhoton(
  query: string,
  dLat: number | null | undefined,
  dLng: number | null | undefined
): Promise<LocationOption[]> {
  let url = `https://photon.komoot.io/api/?q=${encodeURIComponent(query)}&limit=5&lang=en`;
  if (dLat != null && dLng != null && dLat !== 0 && dLng !== 0) {
    url += `&lat=${dLat}&lon=${dLng}`;
  }
  const r = await fetch(url);
  if (!r.ok) return [];
  const data = await r.json();
  return (data.features ?? [])
    .map((f: any, i: number) => {
      const p = f.properties ?? {};
      const coords = f.geometry?.coordinates ?? [0, 0];
      const nameParts = [p.name, p.street, p.city].filter(Boolean);
      return {
        id: -(i + 1),
        name: nameParts.join(", ") || p.label || "Unknown",
        lat: coords[1] as number,
        lng: coords[0] as number,
        type: "geo",
        source: "geo" as const
      };
    })
    .filter((o: any) => o.lat !== 0 && o.lng !== 0);
}

async function geocodeNominatim(
  query: string,
  dLat: number | null | undefined,
  dLng: number | null | undefined
): Promise<LocationOption[]> {
  let url = `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(query)}&limit=5&countrycodes=my`;
  if (dLat != null && dLng != null && dLat !== 0 && dLng !== 0) {
    const d = 5;
    url += `&viewbox=${dLng - d},${dLat - d},${dLng + d},${dLat + d}&bounded=0`;
  }
  const r = await fetch(url, { headers: { "Accept-Language": "en" } });
  if (!r.ok) return [];
  const data: any[] = await r.json();
  return data.map((item, i) => {
    const parts = (item.display_name as string)
      .split(",")
      .map((s: string) => s.trim())
      .filter((s: string) => s && !/^\d{5}$/.test(s)); // strip bare postcodes
    return {
      id: -(1000 + i + 1),
      name: parts.slice(0, 4).join(", "),
      lat: parseFloat(item.lat),
      lng: parseFloat(item.lon),
      type: "geo",
      source: "geo" as const
    };
  });
}

async function geocode(query: string): Promise<LocationOption[]> {
  if (query.trim().length < 2) return [];
  const dLat = props.deviceLat;
  const dLng = props.deviceLng;
  const [photon, nominatim] = await Promise.all([
    geocodePhoton(query, dLat, dLng).catch(() => [] as LocationOption[]),
    geocodeNominatim(query, dLat, dLng).catch(() => [] as LocationOption[])
  ]);
  // Merge Nominatim results that aren't already within ~100 m of a Photon result
  const merged = [...photon];
  for (const nom of nominatim) {
    const isDup = photon.some(
      (p) => Math.abs(p.lat - nom.lat) < 0.001 && Math.abs(p.lng - nom.lng) < 0.001
    );
    if (!isDup) merged.push(nom);
  }
  return merged.slice(0, 8);
}

const startMenuOpen = ref(false);
const endMenuOpen = ref(false);

function searchLocations(
  val: string,
  opts: typeof startOptions,
  timer: { value: ReturnType<typeof setTimeout> | null }
) {
  if (timer.value) clearTimeout(timer.value);
  const q = val.toLowerCase();
  const saved = q
    ? savedLocations.value.filter((l) => l.name.toLowerCase().includes(q))
    : savedLocations.value;
  opts.value = saved;
  if (val.trim().length >= 2) {
    timer.value = setTimeout(async () => {
      const geo = await geocode(val);
      opts.value = [...saved, ...geo.filter((g) => !saved.some((s) => s.name === g.name))];
    }, 500);
  }
}

const startTimer = { value: null as ReturnType<typeof setTimeout> | null };
const endTimer = { value: null as ReturnType<typeof setTimeout> | null };

function onStartType(v: string | number | null) {
  const s = String(v ?? "");
  model.value = { ...model.value, start_location: s, start_lat: null, start_lng: null };
  searchLocations(s, startOptions, startTimer);
  startMenuOpen.value = true;
}

function onEndType(v: string | number | null) {
  const s = String(v ?? "");
  model.value = { ...model.value, end_location: s, end_lat: null, end_lng: null };
  searchLocations(s, endOptions, endTimer);
  endMenuOpen.value = true;
}

function selectStart(opt: LocationOption) {
  model.value = {
    ...model.value,
    start_location: opt.name,
    start_lat: opt.lat,
    start_lng: opt.lng
  };
  startMenuOpen.value = false;
}

function clearStart() {
  model.value = { ...model.value, start_location: "", start_lat: null, start_lng: null };
  startOptions.value = [...savedLocations.value];
}

function clearEnd() {
  model.value = { ...model.value, end_location: "", end_lat: null, end_lng: null };
  endOptions.value = [...savedLocations.value];
}

function selectEnd(opt: LocationOption) {
  model.value = { ...model.value, end_location: opt.name, end_lat: opt.lat, end_lng: opt.lng };
  endMenuOpen.value = false;
}

const props = defineProps<{
  modelValue: TripSettings;
  deviceLat?: number | null;
  deviceLng?: number | null;
  disable?: boolean;
}>();
const emit = defineEmits<{ (e: "update:modelValue", v: TripSettings): void }>();

const model = computed<TripSettings>({
  get: () => props.modelValue,
  set: (v) => emit("update:modelValue", v)
});

function update<K extends keyof TripSettings>(key: K, value: TripSettings[K]) {
  model.value = { ...model.value, [key]: value };
}

function pad2(n: number): string {
  return String(n).padStart(2, "0");
}

function parseEstimatedTimeParts(raw: string | undefined | null): { hour: string; minute: string } {
  const s = raw == null ? "" : String(raw).trim();
  if (!s) return { hour: "00", minute: "00" };

  // Expected: "HH:mm"
  if (s.includes(":")) {
    const parts = s.split(":");
    const hh = Number(parts[0]);
    const mm = Number(parts[1]);
    if (Number.isFinite(hh) && Number.isFinite(mm)) {
      return {
        hour: pad2(Math.max(0, Math.floor(hh))),
        minute: pad2(Math.min(59, Math.max(0, Math.floor(mm))))
      };
    }
  }

  // Fallback: numeric minutes
  const n = Number(s);
  if (Number.isFinite(n) && n > 0) {
    const totalSeconds = n * 60;
    const hh = Math.floor(totalSeconds / 3600);
    const mm = Math.floor((totalSeconds % 3600) / 60);
    return { hour: pad2(hh), minute: pad2(mm) };
  }

  return { hour: "00", minute: "00" };
}

const hourOptions = Array.from({ length: 24 }, (_, i) => pad2(i));
const minuteOptions = Array.from({ length: 60 }, (_, i) => pad2(i));

const estimatedHour = computed<string>({
  get: () => {
    const p = parseEstimatedTimeParts(model.value.estimated_time);
    return p.hour;
  },
  set: (v) => {
    const p = parseEstimatedTimeParts(model.value.estimated_time);
    update("estimated_time", `${v}:${p.minute}`);
  }
});

const estimatedMinute = computed<string>({
  get: () => {
    const p = parseEstimatedTimeParts(model.value.estimated_time);
    return p.minute;
  },
  set: (v) => {
    const p = parseEstimatedTimeParts(model.value.estimated_time);
    update("estimated_time", `${p.hour}:${v}`);
  }
});
</script>

<template>
  <div class="q-pa-md">
    <!-- Shipment Info -->
    <div class="row q-col-gutter-sm items-end q-mb-md">
      <LabelApp label="Shipment ID" class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          :disable="props.disable"
          :model-value="model.shipment_id"
          @update:model-value="(v) => update('shipment_id', String(v || ''))"
        />
      </LabelApp>
      <LabelApp label="Goods" class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          :disable="props.disable"
          :model-value="model.goods"
          @update:model-value="(v) => update('goods', String(v || ''))"
        />
      </LabelApp>
      <LabelApp label="Carrier" class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          :disable="props.disable"
          :model-value="model.carrier"
          @update:model-value="(v) => update('carrier', String(v || ''))"
        />
      </LabelApp>
      <LabelApp label="Sender" class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          :disable="props.disable"
          :model-value="model.sender"
          @update:model-value="(v) => update('sender', String(v || ''))"
        />
      </LabelApp>
      <LabelApp label="Truck Name" class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          :disable="props.disable"
          :model-value="model.truck_name"
          @update:model-value="(v) => update('truck_name', String(v || ''))"
        />
      </LabelApp>
      <LabelApp label="Estimated Time" class="col col-12 col-sm-4 col-md-4">
        <div class="row q-col-gutter-sm items-center" style="width: 100%">
          <q-select
            ref="hourSelectRef"
            dense
            outlined
            :disable="props.disable"
            v-model="estimatedHour"
            :options="hourOptions"
            placeholder="HH"
            style="width: 50%"
            @popup-show="onTimeSelectShow"
            @popup-hide="onTimeSelectHide"
          />
          <q-select
            ref="minuteSelectRef"
            dense
            outlined
            :disable="props.disable"
            v-model="estimatedMinute"
            :options="minuteOptions"
            placeholder="MM"
            style="width: 50%"
            @popup-show="onTimeSelectShow"
            @popup-hide="onTimeSelectHide"
          />
        </div>
      </LabelApp>
      <LabelApp label="Customer Company" class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          :disable="props.disable"
          :model-value="model.customer_company"
          @update:model-value="(v) => update('customer_company', String(v || ''))"
        />
      </LabelApp>
      <LabelApp label="Asset ID" class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          :disable="props.disable"
          :model-value="model.asset_id"
          @update:model-value="(v) => update('asset_id', String(v || ''))"
        />
      </LabelApp>
      <LabelApp label="Transport Route" class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          :disable="props.disable"
          :model-value="model.transport_route"
          @update:model-value="(v) => update('transport_route', String(v || ''))"
        />
      </LabelApp>
      <LabelApp label="Receiver" class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          :disable="props.disable"
          :model-value="model.receiver"
          @update:model-value="(v) => update('receiver', String(v || ''))"
        />
      </LabelApp>
      <LabelApp label="Remark" class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          type="textarea"
          autogrow
          :disable="props.disable"
          :model-value="model.remark"
          @update:model-value="(v) => update('remark', String(v || ''))"
        />
      </LabelApp>
    </div>

    <q-separator class="q-mb-md" />

    <!-- Trip Rules -->
    <div class="row q-col-gutter-sm items-end">
      <LabelApp label="Start Location" class="col col-12 col-sm-4 col-md-4">
        <!-- Confirmed selection pill — coordinates folded inside, no extra row -->
        <div
          v-if="model.start_lat"
          class="row items-center no-wrap"
          style="
            border: 1px solid #c8e6c9;
            border-radius: 4px;
            padding: 5px 10px;
            background: #f1f8e9;
            height: 40px;
            gap: 6px;
            box-sizing: border-box;
          "
        >
          <q-icon name="fa-solid fa-map-pin" color="positive" size="12px" style="flex-shrink: 0" />
          <div class="col" style="min-width: 0; overflow: hidden">
            <div
              class="ellipsis"
              style="font-size: 12px; color: #2e7d32; font-weight: 500; line-height: 1.25"
            >
              {{ model.start_location }}
            </div>
            <div class="text-grey-5" style="font-size: 10px; line-height: 1.2">
              {{ model.start_lat.toFixed(5) }}, {{ model.start_lng?.toFixed(5) }}
            </div>
          </div>
          <q-btn
            flat
            round
            dense
            size="xs"
            icon="close"
            color="grey-5"
            style="flex-shrink: 0"
            :disable="props.disable"
            @click="clearStart"
          />
        </div>
        <!-- Search input -->
        <div v-else class="relative-position">
          <q-input
            dense
            outlined
            clearable
            :disable="props.disable"
            :model-value="model.start_location"
            placeholder="Type a place or address…"
            @update:model-value="
              (v) => {
                if (!v && v !== 0) {
                  model = { ...model, start_location: '', start_lat: null, start_lng: null };
                  startMenuOpen = false;
                } else {
                  onStartType(v);
                }
              }
            "
            @focus="
              () => {
                searchLocations(model.start_location || '', startOptions, startTimer);
                startMenuOpen = true;
              }
            "
            @keydown.esc.stop="startMenuOpen = false"
          />
          <q-menu
            v-model="startMenuOpen"
            no-parent-event
            no-focus
            fit
            :offset="[0, 2]"
            max-height="280px"
            scroll-target=".q-page"
          >
            <q-list dense>
              <q-item v-for="opt in startOptions" :key="opt.id" clickable @click="selectStart(opt)">
                <q-item-section avatar style="min-width: 28px">
                  <q-icon
                    :name="
                      opt.source === 'saved' ? 'fa-solid fa-bookmark' : 'fa-solid fa-location-dot'
                    "
                    :color="opt.source === 'saved' ? 'primary' : 'grey-5'"
                    size="11px"
                  />
                </q-item-section>
                <q-item-section>
                  <q-item-label style="font-size: 12px; white-space: normal; line-height: 1.3">
                    {{ opt.name }}
                  </q-item-label>
                  <q-item-label caption style="font-size: 10px">
                    {{ opt.source === "saved" ? opt.type : "Map result" }}
                  </q-item-label>
                </q-item-section>
              </q-item>
              <q-item v-if="!startOptions.length">
                <q-item-section class="text-grey" style="font-size: 12px">
                  {{
                    model.start_location && model.start_location.length >= 2
                      ? "Searching…"
                      : "Type to search for a location"
                  }}
                </q-item-section>
              </q-item>
            </q-list>
          </q-menu>
        </div>
      </LabelApp>
      <LabelApp label="End Location" class="col col-12 col-sm-4 col-md-4">
        <!-- Confirmed selection pill -->
        <div
          v-if="model.end_lat"
          class="row items-center no-wrap"
          style="
            border: 1px solid #c8e6c9;
            border-radius: 4px;
            padding: 5px 10px;
            background: #f1f8e9;
            height: 40px;
            gap: 6px;
            box-sizing: border-box;
          "
        >
          <q-icon name="fa-solid fa-map-pin" color="positive" size="12px" style="flex-shrink: 0" />
          <div class="col" style="min-width: 0; overflow: hidden">
            <div
              class="ellipsis"
              style="font-size: 12px; color: #2e7d32; font-weight: 500; line-height: 1.25"
            >
              {{ model.end_location }}
            </div>
            <div class="text-grey-5" style="font-size: 10px; line-height: 1.2">
              {{ model.end_lat.toFixed(5) }}, {{ model.end_lng?.toFixed(5) }}
            </div>
          </div>
          <q-btn
            flat
            round
            dense
            size="xs"
            icon="close"
            color="grey-5"
            style="flex-shrink: 0"
            :disable="props.disable"
            @click="clearEnd"
          />
        </div>
        <!-- Search input -->
        <div v-else class="relative-position">
          <q-input
            dense
            outlined
            clearable
            :disable="props.disable"
            :model-value="model.end_location"
            placeholder="Type a place or address…"
            @update:model-value="
              (v) => {
                if (!v && v !== 0) {
                  model = { ...model, end_location: '', end_lat: null, end_lng: null };
                  endMenuOpen = false;
                } else {
                  onEndType(v);
                }
              }
            "
            @focus="
              () => {
                searchLocations(model.end_location || '', endOptions, endTimer);
                endMenuOpen = true;
              }
            "
            @keydown.esc.stop="endMenuOpen = false"
          />
          <q-menu
            v-model="endMenuOpen"
            no-parent-event
            no-focus
            fit
            :offset="[0, 2]"
            max-height="280px"
            scroll-target=".q-page"
          >
            <q-list dense>
              <q-item v-for="opt in endOptions" :key="opt.id" clickable @click="selectEnd(opt)">
                <q-item-section avatar style="min-width: 28px">
                  <q-icon
                    :name="
                      opt.source === 'saved' ? 'fa-solid fa-bookmark' : 'fa-solid fa-location-dot'
                    "
                    :color="opt.source === 'saved' ? 'primary' : 'grey-5'"
                    size="11px"
                  />
                </q-item-section>
                <q-item-section>
                  <q-item-label style="font-size: 12px; white-space: normal; line-height: 1.3">
                    {{ opt.name }}
                  </q-item-label>
                  <q-item-label caption style="font-size: 10px">
                    {{ opt.source === "saved" ? opt.type : "Map result" }}
                  </q-item-label>
                </q-item-section>
              </q-item>
              <q-item v-if="!endOptions.length">
                <q-item-section class="text-grey" style="font-size: 12px">
                  {{
                    model.end_location && model.end_location.length >= 2
                      ? "Searching…"
                      : "Type to search for a location"
                  }}
                </q-item-section>
              </q-item>
            </q-list>
          </q-menu>
        </div>
      </LabelApp>
      <LabelApp label="Max Trip Duration (min)" class="col col-12 col-sm-4 col-md-4">
        <q-input
          dense
          outlined
          type="number"
          min="0"
          placeholder="Leave blank to use Estimated Time"
          :disable="props.disable"
          :model-value="model.trip_max_minutes"
          @update:model-value="
            (v) => update('trip_max_minutes', v === '' || v == null ? null : Number(v))
          "
        />
      </LabelApp>
      <div class="col col-12 col-sm-4 col-md-4 flex items-center">
        <q-toggle
          dense
          label="Geofence Required"
          :disable="props.disable"
          :model-value="model.geofence_required"
          @update:model-value="(v) => update('geofence_required', !!v)"
        />
      </div>
    </div>
  </div>
</template>
