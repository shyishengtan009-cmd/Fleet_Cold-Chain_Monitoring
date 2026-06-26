import { createRouter, createWebHistory } from "vue-router";

// Trimmed from the real HIAS router (3000+ lines covering every module) down to just
// the 4 Fleet routes. Paths kept identical to the real app
// (/monitoring/tt19-fleet/...) since NavigationSideV2's fallback menu and
// useAlarmNotifier's urlLink both hardcode this path shape.
const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: "/", redirect: "/monitoring/tt19-fleet/dashboard" },
    {
      path: "/monitoring/tt19-fleet/dashboard",
      name: "FleetDashboard",
      component: () =>
        import("@/components/Monitoring/TemperatureHumidity/Fleet/FleetDashboard.vue")
    },
    {
      path: "/monitoring/tt19-fleet/device-settings",
      name: "FleetDeviceSettings",
      component: () =>
        import("@/components/Monitoring/TemperatureHumidity/FleetDeviceSettings/DeviceSettings.vue")
    },
    {
      path: "/monitoring/tt19-fleet/real-time",
      name: "FleetRealTime",
      component: () =>
        import("@/components/Monitoring/TemperatureHumidity/FleetRealTimeMonitoring/RtmMain.vue")
    },
    {
      path: "/monitoring/tt19-fleet/alert",
      name: "FleetAlert",
      component: () => import("@/components/Monitoring/TemperatureHumidity/FleetAlert/AlertPage.vue")
    }
  ]
});

export default router;
