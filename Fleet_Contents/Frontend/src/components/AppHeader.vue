<script setup lang="ts">
// Visually modeled on the real app's NavigationTopV2.vue (logo, global search, org badge,
// notification bell, user avatar+menu) but with zero functional wiring — no search store, no
// notification API, no org-switching, no real auth. Every interactive-looking element here is
// either a no-op or a purely cosmetic q-menu. This exists so the demo's chrome looks like a real
// product shell, not because any of these other modules actually exist in this standalone repo.
defineProps<{ modelValue: boolean }>();
const emit = defineEmits<{ "update:modelValue": [boolean] }>();

const corporateFareIconSVG = `<svg xmlns="http://www.w3.org/2000/svg" height="22px" viewBox="0 0 24 24" width="20px">
  <rect fill="none" height="30" width="28" />
  <path fill="currentColor" d="M12,7V3H2v18h20V7H12z M10,19H4v-2h6V19z M10,15H4v-2h6V15z M10,11H4V9h6V11z M10,7H4V5h6V7z M20,19h-8V9h8V19z M18,11h-4v2 h4V11z M18,15h-4v2h4V15z" />
</svg>`;
const userAvatarSVG = `<svg width="34px" height="34px" viewBox="0 0 16 16" xmlns="http://www.w3.org/2000/svg" fill="currentColor">
  <path d="M3 14s-1 0-1-1 1-4 6-4 6 3 6 4-1 1-1 1H3zm5-6a3 3 0 1 0 0-6 3 3 0 0 0 0 6z"/>
</svg>`;
</script>

<template>
  <div class="row items-center justify-between" style="padding: 0 12px; min-height: 60px">
    <div class="row no-wrap items-center">
      <q-btn
        flat
        round
        dense
        icon="fa-solid fa-bars"
        @click="emit('update:modelValue', !modelValue)"
      />
      <div class="row items-center q-ml-sm cursor-pointer">
        <svg width="30" height="30" viewBox="0 0 30 30" class="q-mr-sm">
          <rect x="0" y="0" width="30" height="9" rx="2" fill="#0ea5e9" />
          <rect x="0" y="10.5" width="30" height="9" rx="2" fill="#16a34a" />
          <rect x="0" y="21" width="30" height="9" rx="2" fill="#1f2a58" />
        </svg>
        <span class="text-weight-bold" style="font-size: 20px; letter-spacing: 0.5px">FLEET</span>
      </div>
    </div>

    <div class="row items-center gap-md">
      <q-input outlined dense disable style="width: 240px" class="gt-xs" placeholder="Search">
        <q-tooltip>Demo only — search isn't wired up</q-tooltip>
        <template #append><q-icon size="14px" name="fa-solid fa-magnifying-glass" /></template>
      </q-input>

      <div class="row items-center gap-sm gt-sm">
        <span v-html="corporateFareIconSVG" />
        <span class="text-weight-medium">Demo Org</span>
        <q-icon name="fa-solid fa-chevron-down" size="10px" />
      </div>

      <q-btn round flat style="background-color: #f2f2f2">
        <q-icon name="fa-solid fa-bell" size="14px" style="color: #aaaaaa" />
        <q-menu>
          <q-list style="min-width: 220px">
            <q-item-label header>Notifications</q-item-label>
            <q-item><q-item-section class="text-grey">No notifications (demo)</q-item-section></q-item>
          </q-list>
        </q-menu>
      </q-btn>

      <q-btn flat no-caps style="padding: 4px 8px; border-radius: 8px">
        <q-avatar size="34px">
          <div style="border: 1.5px solid; border-radius: 100px; overflow: hidden; width: 34px; height: 34px">
            <span v-html="userAvatarSVG" style="display: flex; align-items: center; transform: translateY(3px)" />
          </div>
        </q-avatar>
        <div class="q-ml-sm gt-xs text-left">
          <div>Demo User</div>
          <div class="text-body2 text-grey-7">Viewer</div>
        </div>
        <q-icon name="fa-solid fa-angle-down" size="12px" class="q-ml-xs gt-xs" />
        <q-menu>
          <q-list style="min-width: 160px">
            <q-item><q-item-section class="text-grey">No account system (demo)</q-item-section></q-item>
          </q-list>
        </q-menu>
      </q-btn>
    </div>
  </div>
</template>
