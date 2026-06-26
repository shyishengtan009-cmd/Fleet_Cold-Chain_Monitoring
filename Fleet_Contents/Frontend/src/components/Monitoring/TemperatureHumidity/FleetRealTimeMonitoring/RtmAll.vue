<script setup lang="ts">
const props = defineProps<{
  trip: any;
}>();

function val(v: any): string {
  if (v === null || v === undefined || v === "") return "—";
  return String(v);
}
</script>

<template>
  <q-card flat>
    <q-card-section class="headerColor text-weight-bold">Shipment Information</q-card-section>
    <q-separator />
    <q-card-section>
      <div :class="$style.shipmentGrid">
        <div :class="$style.shipCol">
          <div :class="$style.shipRow">
            <div :class="$style.shipKey">Shipment ID</div>
            <div :class="$style.shipVal">{{ val(props.trip?.shipment_id) }}</div>
          </div>
          <div :class="$style.shipRow">
            <div :class="$style.shipKey">Goods</div>
            <div :class="$style.shipVal">{{ val(props.trip?.goods) }}</div>
          </div>
          <div :class="$style.shipRow">
            <div :class="$style.shipKey">Carrier</div>
            <div :class="$style.shipVal">{{ val(props.trip?.carrier) }}</div>
          </div>
          <div :class="$style.shipRow">
            <div :class="$style.shipKey">Sender</div>
            <div :class="$style.shipVal">{{ val(props.trip?.sender) }}</div>
          </div>
          <div :class="$style.shipRow">
            <div :class="$style.shipKey">Truck Name</div>
            <div :class="$style.shipVal">{{ val(props.trip?.truck_name) }}</div>
          </div>
          <div :class="$style.shipRow">
            <div :class="$style.shipKey">Estimated Time</div>
            <div :class="$style.shipVal">{{ val(props.trip?.estimated_time) }}</div>
          </div>
          <div :class="$style.shipRow">
            <div :class="$style.shipKey">Remark</div>
            <div :class="$style.shipVal">{{ val(props.trip?.remark) }}</div>
          </div>
          <div :class="$style.shipRow">
            <div :class="$style.shipKey">Email</div>
            <div :class="$style.shipVal">{{ val(props.trip?.notify_email) }}</div>
          </div>
        </div>
        <div :class="$style.shipCol">
          <div :class="$style.shipRow">
            <div :class="$style.shipKey">Customer Company</div>
            <div :class="$style.shipVal">{{ val(props.trip?.customer_company) }}</div>
          </div>
          <div :class="$style.shipRow">
            <div :class="$style.shipKey">Asset ID</div>
            <div :class="$style.shipVal">{{ val(props.trip?.asset_id) }}</div>
          </div>
          <div :class="$style.shipRow">
            <div :class="$style.shipKey">Transport Route</div>
            <div :class="$style.shipVal">{{ val(props.trip?.transport_route) }}</div>
          </div>
          <div :class="$style.shipRow">
            <div :class="$style.shipKey">Receiver</div>
            <div :class="$style.shipVal">{{ val(props.trip?.receiver) }}</div>
          </div>
          <div :class="$style.shipRow">
            <div :class="$style.shipKey">Start Location</div>
            <div :class="$style.shipVal">{{ val(props.trip?.start_location) }}</div>
          </div>
          <div :class="$style.shipRow">
            <div :class="$style.shipKey">End Location</div>
            <div :class="$style.shipVal">{{ val(props.trip?.end_location) }}</div>
          </div>
        </div>
      </div>
    </q-card-section>
  </q-card>
</template>

<style lang="sass" module>
@import '../../../../style/_variables'

.shipmentGrid
  display: grid
  grid-template-columns: 1fr 1fr
  gap: 40px

.shipCol
  display: flex
  flex-direction: column
  gap: 12px

.shipRow
  display: flex
  flex-direction: column

.shipKey
  font-size: 13px
  color: $secondary-grey-1

.shipVal
  font-size: 15px
  font-weight: 500
  margin-top: 2px
</style>
