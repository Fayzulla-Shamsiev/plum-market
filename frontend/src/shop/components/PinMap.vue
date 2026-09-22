<script setup lang="ts">
import L from 'leaflet'
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { t } from '../i18n'
import SIcon from './SIcon.vue'

// Tap the map to drop a pin at the delivery address. Branches are shown for orientation.
// The pin's coordinates go with the order (the admin's orders map uses them); the address text is typed separately.
const pin = defineModel<{ lat: number; lng: number } | null>({ required: true })
const props = defineProps<{ branches: { id: number; name: string; lat: number; lng: number }[] }>()
const el = ref<HTMLElement>()
const locating = ref(false)
let map: L.Map | undefined
let marker: L.Marker | undefined

const pinIcon = L.divIcon({
  className: 'pm-pin',
  html: '<svg width="34" height="44" viewBox="0 0 34 44"><path d="M17 43s15-14.2 15-26A15 15 0 0 0 2 17c0 11.8 15 26 15 26z" fill="#1f7aec" stroke="#fff" stroke-width="2.5"/><circle cx="17" cy="17" r="5.5" fill="#fff"/></svg>',
  iconSize: [34, 44], iconAnchor: [17, 43],
})
const branchIcon = L.divIcon({ className: 'pm-branch', html: '<span>🏪</span>', iconSize: [28, 28], iconAnchor: [14, 14] })

function place(lat: number, lng: number) {
  pin.value = { lat: Math.round(lat * 1e6) / 1e6, lng: Math.round(lng * 1e6) / 1e6 }
}
function sync(p: { lat: number; lng: number } | null) {
  if (!map) return
  if (!p) { marker?.remove(); marker = undefined; return }
  if (!marker) {
    marker = L.marker([p.lat, p.lng], { icon: pinIcon, draggable: true }).addTo(map)
    marker.on('dragend', () => { const ll = marker!.getLatLng(); place(ll.lat, ll.lng) })
  } else marker.setLatLng([p.lat, p.lng])
}
watch(pin, sync)

onMounted(() => {
  map = L.map(el.value!, { scrollWheelZoom: false, attributionControl: true }).setView(pin.value ? [pin.value.lat, pin.value.lng] : [41.31, 69.27], pin.value ? 15 : 11)
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19, attribution: '© OpenStreetMap' }).addTo(map)
  for (const b of props.branches) L.marker([b.lat, b.lng], { icon: branchIcon, title: b.name }).addTo(map)
  map.on('click', e => place(e.latlng.lat, e.latlng.lng))
  sync(pin.value)
})
onBeforeUnmount(() => map?.remove())

function locate() {
  if (!navigator.geolocation) return
  locating.value = true
  navigator.geolocation.getCurrentPosition(
    pos => {
      locating.value = false
      place(pos.coords.latitude, pos.coords.longitude)
      map?.setView([pos.coords.latitude, pos.coords.longitude], 16)
    },
    () => { locating.value = false },
    { enableHighAccuracy: true, timeout: 8000 },
  )
}
</script>

<template>
  <div class="pinmap">
    <div ref="el" class="map" />
    <div class="bar">
      <span class="hint">{{ t('pinOnMap') }}</span>
      <button type="button" class="mini" :disabled="locating" @click="locate"><SIcon name="pin" :size="15" /> {{ t('myLocation') }}</button>
      <button v-if="pin" type="button" class="mini" @click="pin = null">{{ t('removePin') }}</button>
    </div>
  </div>
</template>

<style scoped>
.pinmap { border-radius: 16px; overflow: hidden; border: 1px solid var(--line); }
.map { height: 240px; z-index: 0; }
.bar { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; padding: 8px 10px; background: var(--page); font-size: 13px; }
.hint { flex: 1; color: var(--ink-2); min-width: 160px; }
.mini { display: inline-flex; align-items: center; gap: 5px; height: 32px; padding: 0 10px; border-radius: 9px; border: 1px solid var(--line); background: var(--card); cursor: pointer; font-size: 13px; font-weight: 550; }
.mini:hover { border-color: var(--blue); color: var(--blue); }
:deep(.pm-pin), :deep(.pm-branch) { background: none; border: 0; }
:deep(.pm-branch span) { display: grid; place-items: center; width: 28px; height: 28px; border-radius: 50%; background: #fff; box-shadow: 0 1px 4px rgb(0 0 0 / 25%); font-size: 15px; }
</style>
