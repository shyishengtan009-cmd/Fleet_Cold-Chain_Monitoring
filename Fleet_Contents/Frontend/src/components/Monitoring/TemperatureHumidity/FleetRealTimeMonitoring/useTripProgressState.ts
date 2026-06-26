import { ref } from "vue";

export interface TripProgressState {
  status: string;
  duration: string;
  startTimeIso: string | null;
}

export const tripProgressState = ref<TripProgressState>({
  status: "Stopped",
  duration: "00:00:00",
  startTimeIso: null
});
