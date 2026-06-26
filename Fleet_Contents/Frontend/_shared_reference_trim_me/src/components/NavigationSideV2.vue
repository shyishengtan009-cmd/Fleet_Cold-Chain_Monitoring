<script setup lang="ts">
import { Ref, ref, inject, computed, watch } from "vue";
import { useMenuStore } from "../store/menu";
import { MenuDTO } from "../models/Menu";
import { useRoute, useRouter } from "vue-router";
import { useThemeStore } from "@/store/theme";
import { onMounted } from "vue";
import { useCurrentUserStore } from "@/store/currentUser";
import api from "@/helpers/api";

const currentUserStore = useCurrentUserStore();
const themeStore = useThemeStore();
const menuStore = useMenuStore();

const fleetMenus = ref<MenuDTO[]>([]);

const getMenus = computed(() =>
  (menuStore.getMenus.value ?? []).map((menu) => {
    if (menu.name !== "Monitoring" || fleetMenus.value.length === 0) return menu;
    const children = [...((menu.children as MenuDTO[]) ?? [])];
    for (const r of fleetMenus.value) {
      if (!children.some((c) => c.route === r.route)) children.push(r);
    }
    return { ...menu, children };
  })
);
const route = useRoute();
const router = useRouter();
const openMenus = ref<string[]>([]); // 'Open' status of parent menus
const activeMenu = ref<string | null>(null); // 'Active' status of current menu
const isLightTheme = ref<boolean>(true);
const currentOrgName = ref<string>("");

const { showSideNavigation } = inject("side-nav") as {
  showSideNavigation: Ref<boolean>;
};

const corporateFareIconSVG = `<svg
              xmlns="http://www.w3.org/2000/svg"
              enable-background="new 0 0 24 24"
              height="23px"
              viewBox="0 0 24 24"
              width="23px"
            >
              <rect fill="none" height="30" width="28" />
              <path
                fill="currentColor"
                d="M12,7V3H2v18h20V7H12z M10,19H4v-2h6V19z M10,15H4v-2h6V15z M10,11H4V9h6V11z M10,7H4V5h6V7z M20,19h-8V9h8V19z M18,11h-4v2 h4V11z M18,15h-4v2h4V15z"
              />
            </svg>`;

onMounted(async () => {
  try {
    currentOrgName.value = currentUserStore.getCurrentUserOrgName as string;
  } catch (error: unknown) {
    console.error(error);
  }
  // Always inject fleet routes so they show regardless of which backend is active.
  // The UAT baseURL doesn't have the fleet API, so we can't rely on getNavMenus().
  fleetMenus.value = [
    { id: -3, name: "Fleet Dashboard",              route: "/monitoring/tt19-fleet/dashboard",       sequence: 96 } as any,
    { id: -4, name: "Device Settings",              route: "/monitoring/tt19-fleet/device-settings", sequence: 97 } as any,
    { id: -1, name: "Cold Truck Real-Time Monitoring", route: "/monitoring/tt19-fleet/real-time",    sequence: 98 } as any,
    { id: -2, name: "Alert",                        route: "/monitoring/tt19-fleet/alert",           sequence: 99 } as any,
  ];
  try {
    const res = await api.fleet.getNavMenus() as any;
    if (Array.isArray(res) && res.length > 0) fleetMenus.value = res;
  } catch {
    // fleet backend unavailable — use hardcoded fallback above
  }
});

// To check if parent menu is active
function isParentActive(name: string): boolean {
  return activeMenu.value === name;
}

function isParentActiveMenu(menu: MenuDTO): boolean {
  const menuPath = (menu.children?.[0]?.route ?? menu.route)?.trim();
  const routePath = route.matched?.[0]?.path?.trim();

  return !!(menuPath && routePath && menuPath.includes(routePath));
}

// To check if child menu is active
function isChildActive(childItem: MenuDTO): boolean {
  return route.path === childItem.route;
}

// Only activate the focus parent menu when it's being clicked (Deactivate other parent menus)
function toggleParentMenu(menu: MenuDTO) {
  if (!canExpand(menu)) {
    // Check if whether parent menu has a child menu, otherwise, direct its name to route along with activate it
    if (menu.route) {
      activeMenu.value = menu.name;
      router.push({ path: menu.route });
    }
    return;
  }

  openMenus.value = openMenus.value.includes(menu.name)
    ? openMenus.value.filter((m) => m !== menu.name)
    : [menu.name];
  activeMenu.value = menu.name;
}

function canExpand(menu?: MenuDTO): boolean {
  return !!menu?.children?.length;
}

function goToDashboard() {
  router.push({ path: "/dashboard" });
}

const handleDirectOrg = (event: MouseEvent) => {
  event.stopPropagation();
  router.push("/organization/detail/0");
};

function truncateText(text: string, maxLength: number = 34): string {
  if (text.length <= maxLength) {
    return text;
  }
  return text.slice(0, maxLength - 3) + "...";
}

watch(
  () => themeStore.getIsLightTheme,
  (isLightMode) => {
    isLightTheme.value = isLightMode;
  }
);
</script>

<template>
  <q-drawer
    v-model="showSideNavigation"
    show-if-above
    bordered
    :width="325"
    :class="{
      'dark-theme': !isLightTheme,
      'q-pt-md': true
    }"
  >
    <div v-if="$q.screen.lt.md" class="row content-center q-mb-md q-mt-sm" @click="goToDashboard()">
      <q-img
        src="/hias-logo.png"
        fit="contain"
        style="width: 120px; margin: 0 auto; cursor: pointer"
      />
    </div>

    <div v-if="!$q.screen.gt.md" style="margin: 20px 0 10px 23px">
      <div
        v-if="currentOrgName"
        :class="[!isLightTheme ? 'dark-theme-img-svg' : '', 'row items-center']"
      >
        <span
          v-html="corporateFareIconSVG"
          class="svg-icon"
          @click="handleDirectOrg"
          style="cursor: pointer"
        ></span>

        <span @click="handleDirectOrg" style="cursor: pointer; font-weight: 600; margin-left: 12px">
          {{ truncateText(currentOrgName) }}
        </span>
      </div>
    </div>

    <q-expansion-item
      v-for="menu in getMenus"
      :key="menu.name"
      :class="[isLightTheme ? $style.navItem : $style.darkThemeMenuItem, 'overflow-hidden']"
      :model-value="isParentActive(menu.name) || isParentActiveMenu(menu)"
      :active-class="$style.activeNavItem"
      expand-icon="fa-solid fa-angle-down"
      expanded-icon="fa-solid fa-chevron-up"
      :expand-icon-class="[
        canExpand(menu)
          ? isLightTheme
            ? $style.sideNavExpIcon
            : $style.sideNavExpIconDarkTheme
          : '',
        isParentActiveMenu(menu)
          ? isLightTheme
            ? $style.activeNavItem
            : $style.activeNavItemDarkTheme
          : ''
      ]"
      :hide-expand-icon="!canExpand(menu)"
      :label="menu.name"
      :icon="menu.icon || ''"
      @click="toggleParentMenu(menu)"
    >
      <template #header>
        <q-item :class="[$style.localQitem]">
          <q-item-section
            avatar
            :class="[
              $style.baseBorder,
              isParentActiveMenu(menu)
                ? isLightTheme
                  ? [$style.orangeBorder, $style.activeIcon]
                  : [$style.greenBorder, $style.activeIconDarkTheme]
                : ''
            ]"
          >
            <q-icon
              :name="menu.icon || ''"
              size="xs"
              :class="[isLightTheme ? [$style.menuIcon] : [$style.menuIconDarkTheme]]"
            />
          </q-item-section>

          <q-item-section :class="isParentActiveMenu(menu) ? 'parentActive' : ''">
            <div class="row items-center">{{ menu.name }}</div>
          </q-item-section>
        </q-item>
      </template>

      <q-expansion-item
        v-for="childItem in menu.children"
        :key="childItem.name"
        :class="[
          'overflow-hidden',
          isChildActive(childItem)
            ? isLightTheme
              ? [$style.activeNavItem, $style.orangeBorder, $style.activeIcon]
              : [$style.activeNavItemDarkTheme, $style.activeIconDarkTheme]
            : ''
        ]"
        :model-value="isChildActive(childItem)"
        :active-class="$style.activeNavItem"
        :label="childItem.name"
        :to="childItem.route"
        hide-expand-icon
      >
        <template #header>
          <q-item :class="[$style.localQitem]">
            <q-item-section
              avatar
              :class="[
                $style.baseBorder,
                isChildActive(childItem)
                  ? isLightTheme
                    ? [$style.orangeBorder, $style.activeIcon]
                    : [$style.greenBorder, $style.activeIconDarkTheme]
                  : ''
              ]"
            >
              <q-icon
                :name="childItem.icon || ''"
                size="xs"
                :class="[isLightTheme ? [$style.menuIcon] : [$style.menuIconDarkTheme]]"
              />
            </q-item-section>

            <q-item-section
              :class="[
                isChildActive(childItem)
                  ? isLightTheme
                    ? [$style.childActive]
                    : [$style.childActiveDarkTheme]
                  : ''
              ]"
            >
              <div
                class="row items-center"
                :class="[isLightTheme ? [$style.childInset] : [$style.childInsetDarkTheme]]"
              >
                {{ childItem.name }}
              </div>
            </q-item-section>
          </q-item>
        </template>
      </q-expansion-item>
    </q-expansion-item>
  </q-drawer>
</template>

<style lang="sass" module>
@import '../style/_variables'

:global(.q-item:has(> div.q-item__section.parentActive))
  background-color: $menu-item-green

.sideNavExpIcon
  :global(i.q-icon.fa-angle-down),
  :global(i.q-icon.fa-chevron-up)
    font-size: 14px

  &:global(.q-item__section.q-item__section--side:has(> div.q-item__section.parentActive))
    background-color: $menu-item-green

.localQitem
  width: 100%

  .baseBorder
    border-left: 8px solid transparent
    padding-left: 16px

  .orangeBorder
    border-left: 8px solid $primary-orange

  .activeIcon
    color: $primary-orange

.navItem
  font-weight: 600
  :global(.q-item)
    padding: 0

.activeNavItem
  background-color: $menu-item-green

.childInset
  padding-left: 20px

.darkThemeMenuItem
  font-weight: 600
  :global(.q-item)
    padding: 0

    &:global(.q-item:has(> div.q-item__section.parentActive))
      background-color: $primary-dark-purple
      color: $white

.activeNavItemDarkTheme
  background-color: $primary-dark-purple
  > div > a:first-of-type
    background-color: $primary-dark-purple

.greenBorder
  border-left: 8px solid $primary-green !important

.activeIconDarkTheme
  color: $white

.childInsetDarkTheme
  padding-left: 20px

.menuIconDarkTheme
  color: $secondary-green-1

.sideNavExpIconDarkTheme
  :global(i.q-icon.fa-angle-down),
  :global(i.q-icon.fa-chevron-up)
    font-size: 14px
</style>
