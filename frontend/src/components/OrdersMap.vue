<script setup lang="ts">
import L from 'leaflet'
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import type { Dashboard, OrderStatus } from '../api'
import { money, statusLabel } from '../format'

const props = defineProps<{ data: Dashboard['map'] }>()

const el = ref<HTMLDivElement>()
const showBranches = ref(true)
const showOrders = ref(true)
let map: L.Map | null = null
const orderLayer = L.layerGroup()
const branchLayer = L.layerGroup()

// Three buckets that match the dashboard's order-dynamics chart.
const groupColor = (s: OrderStatus) => (s === 'Completed' ? '#1baf7a' : s === 'Cancelled' ? '#eb6834' : '#2a78d6')

const branchIcon = L.divIcon({
  className: 'branch-pin',
  html: '<svg viewBox="0 0 24 32" width="26" height="34"><path d="M12 0C5.4 0 0 5.3 0 11.9 0 20.8 12 32 12 32s12-11.2 12-20.1C24 5.3 18.6 0 12 0z" fill="#6b2d8c" stroke="#fff" stroke-width="1.5"/><circle cx="12" cy="12" r="4.5" fill="#fff"/></svg>',
  iconSize: [26, 34],
  iconAnchor: [13, 34],
  popupAnchor: [0, -30],
})

function render() {
  if (!map) return
  orderLayer.clearLayers()
  branchLayer.clearLayers()
  for (const o of props.data.orders) {
    L.circleMarker([o.lat, o.lng], {
      radius: 5, weight: 1.5, color: '#fff', fillColor: groupColor(o.status), fillOpacity: 0.85,
    })
      .bindTooltip(`#${o.id} · ${statusLabel[o.status]} · ${money(o.total)}`)
      .addTo(orderLayer)
  }
  for (const b of props.data.branches) {
    L.marker([b.lat, b.lng], { icon: branchIcon, zIndexOffset: 1000 })
      .bindPopup(`<b>${b.name}</b><br>${b.address}`)
      .addTo(branchLayer)
  }
  syncLayers()
  const pts = [...props.data.orders.map(o => [o.lat, o.lng]), ...props.data.branches.map(b => [b.lat, b.lng])] as [number, number][]
  if (pts.length) map.fitBounds(L.latLngBounds(pts), { padding: [24, 24], maxZoom: 13 })
}

function syncLayers() {
  if (!map) return
  showOrders.value ? orderLayer.addTo(map) : orderLayer.remove()
  showBranches.value ? branchLayer.addTo(map) : branchLayer.remove()
}

onMounted(() => {
  map = L.map(el.value!, { scrollWheelZoom: false }).setView([41.31, 69.27], 11)
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 18,
    attribution: '&copy; OpenStreetMap',
  }).addTo(map)
  render()
})
onBeforeUnmount(() => map?.remove())
watch(() => props.data, render)
watch([showBranches, showOrders], syncLayers)
</script>

<template>
  <div class="map-wrap">
    <div class="map-controls">
      <label class="switch"><input v-model="showOrders" type="checkbox" /><span class="track" />Заказы</label>
      <label class="switch"><input v-model="showBranches" type="checkbox" /><span class="track" />Филиалы</label>
      <span class="legend">
        <span><i style="background:#2a78d6" />Новые / в работе</span>
        <span><i style="background:#1baf7a" />Выполненные</span>
        <span><i style="background:#eb6834" />Отменённые</span>
      </span>
    </div>
    <div ref="el" class="map" />
  </div>
</template>

<style scoped>
.map-controls { display: flex; gap: 16px; align-items: center; flex-wrap: wrap; margin-bottom: 10px; font-size: 13px; }
.legend { margin-left: auto; display: flex; gap: 12px; color: var(--text-2); font-size: 12px; flex-wrap: wrap; }
.legend span { display: inline-flex; align-items: center; gap: 5px; }
.legend i { width: 9px; height: 9px; border-radius: 50%; }
.map { height: 340px; border-radius: 8px; border: 1px solid var(--border); z-index: 0; }
:global(.branch-pin) { background: none; border: none; }
</style>
