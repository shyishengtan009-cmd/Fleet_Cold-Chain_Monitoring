<script setup lang="ts">
import { computed } from "vue";

import LabelApp from "./LabelApp.vue";
import { PropType } from "vue";
import { ValidationRule } from "quasar";

// define Emits
const emit = defineEmits(["onChange"]);

// define Props
const props = defineProps({
  label: {
    type: String,
    default: () => ""
  },
  date: {
    type: String,
    default: () => ""
  },
  placeholder: {
    type: String,
    default: () => "DD/MM/YYYY"
  },
  rules: {
    type: Array as PropType<ValidationRule[]>,
    default: () => []
  },
  required: {
    type: Boolean,
    default: false
  }
});

// computed
const value = computed({
  get() {
    return props.date as string;
  },
  set(newValue: string) {
    emit("onChange", newValue);
  }
});
</script>

<template>
  <LabelApp :label="props.label" :required="props.required">
    <q-input
      v-model="value"
      mask="##/##/####"
      outlined
      dense
      no-error-icon
      :rules="props.rules"
      :placeholder="props.placeholder"
    >
      <template #append>
        <q-icon name="fa-solid fa-calendar-days" class="cursor-pointer">
          <q-popup-proxy cover transition-show="scale" transition-hide="scale">
            <q-date v-model="value" mask="DD/MM/YYYY">
              <div class="row items-center justify-end">
                <!-- <q-btn label="Cancel" color="primary" flat v-close-popup /> -->
                <!-- <q-btn label="OK" color="primary" flat @click="save" v-close-popup /> -->
                <q-btn v-close-popup label="Close" color="primary" flat />
              </div>
            </q-date>
          </q-popup-proxy>
        </q-icon>
      </template>
    </q-input>
  </LabelApp>
</template>
