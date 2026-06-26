<script setup lang="ts">
import { ref } from "vue";
import { useRoute, useRouter } from "vue-router";

// Visually modeled on the real app's NavigationSideV2.vue full menu structure (Dashboard,
// Document, Certification, Audit, ... Monitoring, Human Resource, Customer Voice Management),
// but every item EXCEPT the 4 Fleet pages under Monitoring is purely cosmetic — no route, no
// click handler, no backing page. They exist only so the sidebar looks like the real product
// shell this feature was extracted from, not because those other modules exist in this repo.
interface MenuItem {
  label: string;
  icon: string;
  route?: string; // only Fleet items have one
}

const topLevel: MenuItem[] = [
  { label: "Dashboard", icon: "fa-solid fa-chart-pie" },
  { label: "Document", icon: "fa-solid fa-folder" },
  { label: "Certification", icon: "fa-solid fa-certificate" },
  { label: "Audit", icon: "fa-solid fa-clipboard-check" },
  { label: "Non-Conformance", icon: "fa-solid fa-triangle-exclamation" },
  { label: "AI Matching", icon: "fa-solid fa-wand-magic-sparkles" },
  { label: "Operation", icon: "fa-solid fa-briefcase" },
  { label: "Purchasing", icon: "fa-solid fa-file-invoice-dollar" },
  { label: "Collaborator", icon: "fa-solid fa-handshake" },
  { label: "Report", icon: "fa-solid fa-list" }
];

const fleetItems: MenuItem[] = [
  { label: "Fleet Dashboard", icon: "fa-solid fa-gauge", route: "/monitoring/fleet/dashboard" },
  { label: "Device Settings", icon: "fa-solid fa-sliders", route: "/monitoring/fleet/device-settings" },
  { label: "Cold Truck Real-Time Monitoring", icon: "fa-solid fa-map-location-dot", route: "/monitoring/fleet/real-time" },
  { label: "Alert", icon: "fa-solid fa-bell", route: "/monitoring/fleet/alert" }
];

const bottomLevel: MenuItem[] = [
  { label: "Human Resource", icon: "fa-solid fa-users" },
  { label: "Customer Voice Management", icon: "fa-solid fa-comments" }
];

const monitoringExpanded = ref(true);
const route = useRoute();
const router = useRouter();
</script>

<template>
  <q-list padding>
    <q-item v-for="item in topLevel" :key="item.label" class="text-grey-8 text-weight-medium" style="cursor: default; font-weight: 600">
      <q-item-section avatar><q-icon :name="item.icon" /></q-item-section>
      <q-item-section>{{ item.label }}</q-item-section>
      <q-item-section side><q-icon name="fa-solid fa-chevron-down" size="10px" /></q-item-section>
    </q-item>

    <q-expansion-item
      v-model="monitoringExpanded"
      icon="fa-solid fa-eye"
      label="Monitoring"
      header-style="font-weight: 600"
      header-class="text-primary"
      default-opened
    >
      <q-item
        v-for="item in fleetItems"
        :key="item.route"
        clickable
        :active="route.path === item.route"
        active-class="text-primary"
        :style="`padding-left: 56px; ${route.path === item.route ? 'background-color: rgba(229,243,234,1)' : ''}`"
        @click="router.push(item.route!)"
      >
        <q-item-section avatar><q-icon :name="item.icon" size="xs" /></q-item-section>
        <q-item-section>{{ item.label }}</q-item-section>
      </q-item>
    </q-expansion-item>

    <q-item v-for="item in bottomLevel" :key="item.label" class="text-grey-8 text-weight-medium" style="cursor: default; font-weight: 600">
      <q-item-section avatar><q-icon :name="item.icon" /></q-item-section>
      <q-item-section>{{ item.label }}</q-item-section>
      <q-item-section side><q-icon name="fa-solid fa-chevron-down" size="10px" /></q-item-section>
    </q-item>
  </q-list>
</template>
