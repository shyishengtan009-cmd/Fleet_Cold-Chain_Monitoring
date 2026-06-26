<script setup lang="ts">
const props = defineProps<{
  deviceLabel: string;
  saving: boolean;
  msg: string;
  msgIsError: boolean;
  isEditing: boolean;
}>();

const emit = defineEmits<{
  (e: "save"): void;
  (e: "back"): void;
  (e: "edit"): void;
  (e: "cancel"): void;
}>();
</script>

<template>
  <div :class="$style.searchContainer">
    <div class="q-pa-md">
      <!-- Title row -->
      <div class="row items-center q-mb-xs">
        <div :class="$style.pageTitle">{{ props.deviceLabel }}</div>
        <q-space />

        <!-- View mode buttons -->
        <template v-if="!props.isEditing">
          <div class="row q-gutter-sm">
            <q-btn no-caps color="primary" label="Edit" @click="emit('edit')" />
            <q-btn no-caps outline color="primary" @click="emit('back')">
              <q-icon class="q-pr-xs" size="12px" name="fa-solid fa-chevron-left" />
              Back
            </q-btn>
          </div>
        </template>

        <!-- Edit mode buttons -->
        <template v-else>
          <div class="row q-gutter-sm">
            <q-btn
              no-caps
              color="primary"
              label="Save"
              :loading="props.saving"
              @click="emit('save')"
            />
            <q-btn no-caps outline color="primary" label="Cancel" @click="emit('cancel')" />
          </div>
        </template>
      </div>

      <!-- Tab indicator — always visible, matches audit report pattern -->
      <div :class="$style.tabRow">
        <span :class="$style.editTab">Edit</span>
      </div>

      <q-separator />

      <div v-if="props.msg" class="q-mt-sm">
        <q-banner
          dense
          rounded
          :class="props.msgIsError ? 'bg-red-1 text-red-9' : 'bg-green-1 text-green-9'"
        >
          <template #avatar>
            <q-icon
              :name="props.msgIsError ? 'fa-solid fa-circle-xmark' : 'fa-solid fa-circle-check'"
            />
          </template>
          {{ props.msg }}
        </q-banner>
      </div>
    </div>
  </div>
</template>

<style lang="sass" module>
@import '../../../../style/_variables'

.searchContainer
  width: 100%
  background-color: $white
  margin-top: 10px

.pageTitle
  font-size: 20px
  font-weight: 700
  color: $primary-black

.tabRow
  display: flex
  padding-top: 4px

.editTab
  font-size: 14px
  font-weight: 600
  color: var(--q-primary)
  padding: 6px 2px 4px
  border-bottom: 2px solid var(--q-primary)
</style>
