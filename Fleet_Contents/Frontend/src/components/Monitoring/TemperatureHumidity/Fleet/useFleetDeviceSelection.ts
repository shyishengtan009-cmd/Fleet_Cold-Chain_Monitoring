import { ref } from "vue";

const STORAGE_KEY = "fleet_selected_device";

// Module-level — persists across client-side navigation between Fleet pages.
// Initialised from localStorage so the last selected truck survives page refresh.
// Device Settings is intentionally excluded from this shared selection.
const _selectedHardwareId = ref<string | null>(localStorage.getItem(STORAGE_KEY));

export function useFleetDeviceSelection() {
  function setDevice(hw: string | null) {
    _selectedHardwareId.value = hw;
    if (hw) localStorage.setItem(STORAGE_KEY, hw);
    else localStorage.removeItem(STORAGE_KEY);
  }

  return { selectedHardwareId: _selectedHardwareId, setDevice };
}
