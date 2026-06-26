import { createRouter, createWebHistory } from "vue-router";

// Trimmed from the original platform's router (3000+ lines covering every module) down to just
// the 4 Fleet routes. Paths kept identical to the real app
// (/monitoring/fleet/...) since NavigationSideV2's fallback menu and
// useAlarmNotifier's urlLink both hardcode this path shape.
const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: "/", redirect: "/monitoring/fleet/dashboard" },
    {
      path: "/monitoring/fleet/dashboard",
      name: "FleetDashboard",
      component: () =>
        import("@/components/Monitoring/TemperatureHumidity/Fleet/FleetDashboard.vue")
    },
    {
      path: "/monitoring/fleet/device-settings",
      name: "FleetDeviceSettings",
      component: () =>
        import("@/components/Monitoring/TemperatureHumidity/FleetDeviceSettings/DeviceSettings.vue")
    },
    {
      path: "/monitoring/fleet/real-time",
      name: "FleetRealTime",
      component: () =>
        import("@/components/Monitoring/TemperatureHumidity/FleetRealTimeMonitoring/RtmMain.vue")
    },
    {
      path: "/monitoring/fleet/alert",
      name: "FleetAlert",
      component: () => import("@/components/Monitoring/TemperatureHumidity/FleetAlert/AlertPage.vue")
    }
  ]
});

export default router;
