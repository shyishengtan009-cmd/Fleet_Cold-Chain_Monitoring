<script setup lang="ts">
import { ref } from "vue";
import AppHeader from "@/components/AppHeader.vue";
import AppFooter from "@/components/AppFooter.vue";
import Sidebar from "@/components/Sidebar.vue";

const showDrawer = ref(true);
</script>

<template>
  <q-layout view="hHh LpR fFf" class="bg-white">
    <q-header bordered class="bg-white text-dark">
      <AppHeader v-model="showDrawer" />
    </q-header>

    <q-drawer v-model="showDrawer" show-if-above bordered :width="300">
      <Sidebar />
    </q-drawer>

    <q-page-container class="bg-grey-2">
      <!-- flex column so AppFooter always lands at the true bottom of the page's minimum
           height, instead of leaving dead grey space below it on short pages (Device Settings)
           while looking fine on tall pages (Dashboard) purely by accident of having enough
           content to fill the viewport. flex: 1 on the content div is what does the pushing. -->
      <q-page style="display: flex; flex-direction: column">
        <!-- .header-app (used by every Fleet page's HeaderAppV2) has `margin: -24px` so its
             white background bleeds flush to the page edges. These are the REAL classes the
             actual app uses to cancel that margin (q-pt-md-lg = 24px padding-top from the `md`
             breakpoint up, q-pt-xs-md = 16px from `xs` up) — they only work because
             quasar/src/css/flex-addon.sass is now imported in main.ts. An earlier pass mistook
             these for fake/nonexistent classes (they're not in index.sass alone) and replaced
             them with a flat `padding: 24px`, which is NOT what the real app does and doesn't
             reproduce its (admittedly imperfect) sub-`md`-breakpoint behavior. -->
        <div class="q-pt-md-lg q-px-md-lg q-pt-xs-md q-px-xs-md" style="flex: 1">
          <router-view />
        </div>
        <AppFooter />
      </q-page>
    </q-page-container>
  </q-layout>
</template>
