<script setup lang="ts">
import { ref } from "vue";

// define Props
const props = defineProps({
  titlePage: {
    type: String,
    default: ""
  },
  breadcrumbs: {
    type: Array<string>,
    default: []
  },
  redirectPage: {
    type: String,
    default: ""
  }
});

const showModal = ref(false);

function openNavigateModal() {
  showModal.value = true;
}

function closeNavigateModal() {
  showModal.value = false;
}
</script>

<template>
  <div
    :class="[
      'bg-white header-app',
      $q.screen.gt.xs ? 'q-px-xl row items-center justify-between' : 'q-px-lg q-py-md column gap-sm'
    ]"
  >
    <div class="col-auto">
      <div :class="['text-weight-medium', $q.screen.gt.xs ? 'text-h4' : 'text-h5']">
        {{ props.titlePage }}
      </div>

      <q-breadcrumbs separator="|" class="breadcrumb q-mt-sm title-small">
        <q-breadcrumbs-el
          v-for="(breadcrumb, index) in props.breadcrumbs"
          :label="breadcrumb"
          :key="index"
        />
      </q-breadcrumbs>
    </div>

    <div class="col-auto">
      <slot></slot>
    </div>
    <q-btn v-if="redirectPage" style="background-color: #d7d7d7" no-caps @click="openNavigateModal">
      <q-img src="/back-icon.svg" width="18px" height="16px" />
      <div class="col q-pr-xs q-ml-sm text-dark text-weight-bold">Back</div>
    </q-btn>
  </div>

  <template>
    <q-dialog v-model="showModal">
      <q-card>
        <q-card-section class="q-pa-lg">
          <div class="text-dark text-h4 text-weight-bold">Confirm Navigation</div>
          <div class="text-dark text-weight-regular q-mt-md" style="font-size: 16px">
            Changes you made may not be saved.
          </div>
          <div class="text-dark text-weight-regular q-mt-sm" style="font-size: 16px">
            Are you sure you want to navigate away from this page?
          </div>
        </q-card-section>
        <q-card-actions align="right" class="q-pb-lg q-pr-lg">
          <q-btn flat label="Stay on this page" color="primary" @click="closeNavigateModal" />
          <q-btn flat label="Leave this page" color="primary" :href="redirectPage" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </template>
</template>
