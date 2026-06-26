import { defineStore } from "pinia";
import { ref } from "vue";

// Trimmed stand-in for the real HIAS notification store (which pulls in a whole
// separate notification API, localStorage user-session throttling, etc.). The Fleet
// alarm notifier (useAlarmNotifier.ts) only ever pushes onto `unread` — that's the
// only piece it actually needs, so that's all this stub provides.
interface FleetNotification {
  notificationId: number;
  senderName: string;
  userId: number;
  message: string;
  notificationCreationDate: string;
  urlLink: string;
}

export const useNotificationStore = defineStore("notifications", () => {
  const unread = ref<FleetNotification[]>([]);
  return { unread };
});
