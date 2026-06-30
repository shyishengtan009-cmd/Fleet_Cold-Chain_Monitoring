<script setup lang="ts">
import { ref } from "vue";
import { useRoute, useRouter } from "vue-router";

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
  </q-list>
</template>
