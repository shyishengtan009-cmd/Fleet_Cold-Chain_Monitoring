<script setup lang="ts">
import { ref } from "vue";
import { useRoute, useRouter } from "vue-router";

// Visually modeled on the real app's full sidebar menu structure. Every item EXCEPT the
// 4 Fleet pages under Monitoring is purely cosmetic — no route, no click handler, no backing
// page — so only their icons are kept (no labels, no tooltips) to fill out the sidebar without
// looking like dead/broken links.
const topLevelIcons: string[] = [
  "fa-solid fa-chart-pie",
  "fa-solid fa-folder",
  "fa-solid fa-certificate",
  "fa-solid fa-clipboard-check",
  "fa-solid fa-triangle-exclamation",
  "fa-solid fa-wand-magic-sparkles",
  "fa-solid fa-briefcase",
  "fa-solid fa-file-invoice-dollar",
  "fa-solid fa-handshake",
  "fa-solid fa-list"
];

const bottomLevelIcons: string[] = ["fa-solid fa-users", "fa-solid fa-comments"];

interface MenuItem {
  label: string;
  icon: string;
  route: string;
}

const fleetItems: MenuItem[] = [
  { label: "Fleet Dashboard", icon: "fa-solid fa-gauge", route: "/monitoring/fleet/dashboard" },
  { label: "Device Settings", icon: "fa-solid fa-sliders", route: "/monitoring/fleet/device-settings" },
  { label: "Cold Truck Real-Time Monitoring", icon: "fa-solid fa-map-location-dot", route: "/monitoring/fleet/real-time" },
  { label: "Alert", icon: "fa-solid fa-bell", route: "/monitoring/fleet/alert" }
];

const monitoringExpanded = ref(true);
const route = useRoute();
const router = useRouter();
</script>

<template>
  <q-list padding>
    <q-item
      v-for="icon in topLevelIcons"
      :key="icon"
      class="decoy-row"
      style="cursor: default; min-height: 44px"
    >
      <q-item-section avatar class="nav-avatar">
        <q-icon :name="icon" class="decoy-icon" size="15px" />
      </q-item-section>
    </q-item>

    <q-expansion-item
      v-model="monitoringExpanded"
      icon="fa-solid fa-eye"
      label="Monitoring"
      header-style="font-weight: 600; font-size: 12px; min-height: 44px"
      header-class="text-primary decoy-row"
      default-opened
    >
      <q-item
        v-for="item in fleetItems"
        :key="item.route"
        clickable
        :active="route.path === item.route"
        active-class="text-primary fleet-item-active"
        class="fleet-item"
        :style="route.path === item.route ? 'background-color: rgba(229,243,234,1)' : ''"
        @click="router.push(item.route)"
      >
        <q-item-section avatar class="nav-avatar"><q-icon :name="item.icon" size="15px" /></q-item-section>
        <q-item-section class="fleet-item-label">{{ item.label }}</q-item-section>
      </q-item>
    </q-expansion-item>

    <q-item
      v-for="icon in bottomLevelIcons"
      :key="icon"
      class="decoy-row"
      style="cursor: default; min-height: 44px"
    >
      <q-item-section avatar class="nav-avatar">
        <q-icon :name="icon" class="decoy-icon" size="15px" />
      </q-item-section>
    </q-item>
  </q-list>
</template>

<style lang="sass" scoped>
@import '../style/_variables'

.decoy-row, :deep(.decoy-row)
  border-bottom: 1px solid #f0f0f0

.decoy-icon
  color: $secondary-grey-1

.nav-avatar
  min-width: 44px

.fleet-item
  min-height: 38px

.fleet-item-label
  font-size: 12px
  font-weight: 500

.fleet-item-active .fleet-item-label
  font-weight: 600
</style>
