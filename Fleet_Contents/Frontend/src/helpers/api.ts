import axios, { AxiosError, AxiosRequestConfig, AxiosResponse, isAxiosError } from "axios";
import { TOKEN_KEY } from "@/utils/constants";
import { getConfig } from "./config";

// Trimmed from the real HIAS api.ts (which wires up ~80 unrelated modules) down to just
// the Fleet namespace + a stand-in demo login. Route paths copied verbatim from the real
// api.constants.ts so they stay in sync with the backend's [Route] attributes.

const config = await getConfig();
export const baseURL: string = config.baseURL;

const axiosClient = axios.create({
  baseURL: `${baseURL}/api`,
  withCredentials: true
});

interface RequestOptions {
  url: string;
  params?: Record<string, unknown>;
  data?: unknown;
  responseType?: AxiosRequestConfig["responseType"];
}

function withAuthHeader(config: AxiosRequestConfig): AxiosRequestConfig {
  return {
    ...config,
    headers: {
      ...config.headers,
      Authorization: `Bearer ${localStorage.getItem(TOKEN_KEY) ?? ""}`
    }
  };
}

function unwrap(response: AxiosResponse) {
  return response.data;
}

function handleError(error: unknown) {
  if (error instanceof AxiosError || isAxiosError(error)) {
    const message =
      (error as AxiosError<{ message?: string }>).response?.data?.message ?? error.message;
    return Promise.reject(new Error(message));
  }
  return Promise.reject(error);
}

const get = ({ url, params, responseType }: RequestOptions) =>
  axiosClient(withAuthHeader({ url, params, method: "GET", responseType })).then(unwrap).catch(handleError);

const post = ({ url, params, data }: RequestOptions) =>
  axiosClient(withAuthHeader({ url, params, data, method: "POST" })).then(unwrap).catch(handleError);

const put = ({ url, params, data }: RequestOptions) =>
  axiosClient(withAuthHeader({ url, params, data, method: "PUT" })).then(unwrap).catch(handleError);

const deleteData = ({ url, params, data }: RequestOptions) =>
  axiosClient(withAuthHeader({ url, params, data, method: "DELETE" })).then(unwrap).catch(handleError);

// ─── Route paths (copied verbatim from the real api.constants.ts's `fleet` block) ───────
const ROUTES = {
  devices: "/fleet/devices",
  devicesSummary: "/fleet/devices/summary",
  devicesRegister: "/fleet/devices/register",
  devicesUpdate: (hw: string) => `/fleet/devices/${encodeURIComponent(hw)}`,
  devicesDelete: (hw: string) => `/fleet/devices/${encodeURIComponent(hw)}`,
  devicesSeed: "/fleet/devices/seed",
  status: "/fleet/fleet/status",
  historyMeta: "/fleet/history/meta",
  historyRange: "/fleet/history/range",
  historyAggregated: "/fleet/history/aggregated",
  deviceSettings: "/fleet/device_settings",
  deviceSettingsSave: "/fleet/device_settings/save",
  alarmLogRecent: "/fleet/alarm_log/recent",
  alarmLogByDate: "/fleet/alarm_log/by_date",
  alarmLogTest: (hardwareId: string) => `/fleet/alarm_log/test/${hardwareId}`,
  sensorReadings: "/fleet/alarm_log/sensor_readings",
  tripsList: "/fleet/trips/list",
  tripById: (id: number) => `/fleet/trips/${id}`,
  tripsOpen: "/fleet/trips/open",
  tripsClose: (id: number) => `/fleet/trips/${id}/close`,
  tripsSave: "/fleet/trips/save",
  locations: "/fleet/locations",
  locationsSave: "/fleet/locations/save",
  locationsDelete: (id: number) => `/fleet/locations/${id}`,
  navMenus: "/fleet/nav/menus",
  batteryForecast: "/fleet/realtime/battery-forecast",
  breachSummary: "/fleet/alarm_log/breach-summary"
};

const api = {
  // Stand-in for the real HIAS login — see Backend/HIAS-NET-CORE/Controllers/AuthDemoController.cs.
  // main.ts calls this once on boot and stores the JWT under TOKEN_KEY.
  auth: {
    demoLogin: () => post({ url: "/auth/demo-login" }) as Promise<{ token: string }>
  },

  fleet: {
    getDevices: () => get({ url: ROUTES.devices }),

    getDevicesSummary: (limit = 500) => get({ url: ROUTES.devicesSummary, params: { limit } }),

    updateDevice: (
      hardwareId: string,
      payload: {
        label?: string | null;
        device_int_id?: number | null;
        app_id?: string;
        app_key?: string;
        app_secret?: string;
        clear_credentials?: boolean;
      }
    ) => put({ url: ROUTES.devicesUpdate(hardwareId), data: payload }),

    registerDevice: (
      hardwareId: string,
      activationCode: string,
      label: string,
      appId?: string,
      appKey?: string,
      appSecret?: string
    ) =>
      post({
        url: ROUTES.devicesRegister,
        data: {
          hardware_id: hardwareId,
          activation_code: activationCode,
          label,
          app_id: appId,
          app_key: appKey,
          app_secret: appSecret
        }
      }),

    unregisterDevice: (hardwareId: string) => deleteData({ url: ROUTES.devicesDelete(hardwareId) }),

    seedDevice: (hardwareId: string, activationCode: string) =>
      post({
        url: ROUTES.devicesSeed,
        data: { hardware_id: hardwareId, activation_code: activationCode }
      }),

    getFleetStatus: (params: { warn_seconds?: number; offline_seconds?: number; limit?: number }) =>
      get({ url: ROUTES.status, params }),

    getHistoryMeta: (hardwareId: string) =>
      get({ url: ROUTES.historyMeta, params: { hardware_id: hardwareId } }),

    getHistoryRange: (hardwareId: string, startUtc: string, endUtc: string, limit = 20000) =>
      get({
        url: ROUTES.historyRange,
        params: { hardware_id: hardwareId, start_utc: startUtc, end_utc: endUtc, limit }
      }),

    getHistoryAggregated: (hardwareId: string, startUtc: string, endUtc: string, bucketMinutes = 60) =>
      get({
        url: ROUTES.historyAggregated,
        params: {
          hardware_id: hardwareId,
          start_utc: startUtc,
          end_utc: endUtc,
          bucket_minutes: bucketMinutes
        }
      }),

    getDeviceSettings: (hardwareId: string) =>
      get({ url: ROUTES.deviceSettings, params: { hardware_id: hardwareId } }),

    saveDeviceSettings: (payload: Record<string, unknown>) =>
      post({ url: ROUTES.deviceSettingsSave, data: payload }),

    getAlarmLogRecent: (hardwareId: string, since: string, limit = 20) =>
      get({ url: ROUTES.alarmLogRecent, params: { hardware_id: hardwareId, since, limit } }),

    getAlarmLogByDate: (hardwareId: string, date: string, limit = 2000) =>
      get({ url: ROUTES.alarmLogByDate, params: { hardware_id: hardwareId, date, limit } }),

    testAlarm: (hardwareId: string) => post({ url: ROUTES.alarmLogTest(hardwareId) }),

    getSensorReadingsByDate: (hardwareId: string, date: string, limit = 2000) =>
      get({ url: ROUTES.sensorReadings, params: { hardware_id: hardwareId, date, limit } }),

    getTrips: (hardwareId: string, limit = 50) =>
      get({ url: ROUTES.tripsList, params: { hardware_id: hardwareId, limit } }),

    getTripById: (id: number) => get({ url: ROUTES.tripById(id) }),

    openTrip: (hardwareId: string, startTime: string) =>
      post({ url: ROUTES.tripsOpen, data: { hardware_id: hardwareId, start_time: startTime } }),

    closeTrip: (tripId: number, endTime: string) =>
      post({ url: ROUTES.tripsClose(tripId), data: { end_time: endTime } }),

    saveTrip: (payload: Record<string, unknown>) => post({ url: ROUTES.tripsSave, data: payload }),

    getLocations: () => get({ url: ROUTES.locations }),

    saveLocation: (payload: Record<string, unknown>) => post({ url: ROUTES.locationsSave, data: payload }),

    deleteLocation: (id: number) => deleteData({ url: ROUTES.locationsDelete(id) }),

    getNavMenus: () => get({ url: ROUTES.navMenus }),

    getBatteryForecast: (hardwareId: string, windowHours = 48, thresholdPct = 20) =>
      get({
        url: ROUTES.batteryForecast,
        params: { hardware_id: hardwareId, window_hours: windowHours, threshold_pct: thresholdPct }
      }),

    getBreachSummary: (hardwareId: string, days = 30) =>
      get({ url: ROUTES.breachSummary, params: { hardware_id: hardwareId, days } }),

    downloadTripReport: (tripId: number) =>
      get({ url: `/fleet/trips/${tripId}/report.pdf`, responseType: "blob" })
  }
};

export default api;
