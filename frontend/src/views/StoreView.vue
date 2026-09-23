<script setup lang="ts">
import L from 'leaflet'
import { nextTick, onMounted, ref } from 'vue'
import { api, type BranchInput, type BranchRow, type StoreSettings } from '../api'
import Icon from '../components/Icon.vue'
import Modal from '../components/Modal.vue'
import { count } from '../format'

// "Магазин": everything the storefront shows about the store — О нас, contacts, delivery price and terms, return
// terms — plus the order time limit and the branches (pickup points and stock locations).
const s = ref<StoreSettings | null>(null)
const branches = ref<BranchRow[]>([])
const saving = ref(false)
const error = ref('')
const saved = ref(false)

async function load() {
  const [settings, list] = await Promise.all([api.storeSettings(), api.branches()])
  s.value = settings
  branches.value = list
}
onMounted(load)

async function save() {
  if (!s.value) return
  saving.value = true
  error.value = ''
  saved.value = false
  try {
    s.value = await api.saveStoreSettings({ ...s.value, freeDeliveryFrom: s.value.freeDeliveryFrom || null })
    saved.value = true
    setTimeout(() => (saved.value = false), 2500)
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    saving.value = false
  }
}

// ---- Branch editor with a map pin ----
const editing = ref<(BranchInput & { id?: number }) | null>(null)
const branchError = ref('')
const mapEl = ref<HTMLElement>()
let map: L.Map | undefined
let marker: L.Marker | undefined

function openBranch(b?: BranchRow) {
  branchError.value = ''
  editing.value = b
    ? { id: b.id, name: b.name, address: b.address, phone: b.phone, workingHours: b.workingHours, lat: b.lat, lng: b.lng }
    : { name: '', address: '', phone: '', workingHours: 'Ежедневно 08:00–22:00', lat: 0, lng: 0, copyStockFrom: branches.value[0]?.id ?? null }
  nextTick(initMap)
}
function initMap() {
  map?.remove()
  marker = undefined
  const e = editing.value
  if (!mapEl.value || !e) return
  const has = e.lat !== 0 || e.lng !== 0
  map = L.map(mapEl.value).setView(has ? [e.lat, e.lng] : [41.31, 69.27], has ? 15 : 11)
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19, attribution: '© OpenStreetMap' }).addTo(map)
  const place = (lat: number, lng: number) => {
    e.lat = Math.round(lat * 1e6) / 1e6
    e.lng = Math.round(lng * 1e6) / 1e6
    if (marker) marker.setLatLng([e.lat, e.lng])
    else marker = L.circleMarker([e.lat, e.lng], { radius: 9, color: '#fff', weight: 2, fillColor: '#6b2d8c', fillOpacity: 1 }).addTo(map!) as unknown as L.Marker
  }
  if (has) place(e.lat, e.lng)
  map.on('click', ev => place(ev.latlng.lat, ev.latlng.lng))
}
function closeBranch() {
  map?.remove()
  map = undefined
  editing.value = null
}
async function saveBranch() {
  const e = editing.value
  if (!e) return
  branchError.value = ''
  try {
    const body: BranchInput = { name: e.name, address: e.address, phone: e.phone || null, workingHours: e.workingHours || null, lat: e.lat, lng: e.lng, copyStockFrom: e.id ? null : e.copyStockFrom }
    if (e.id) await api.updateBranch(e.id, body)
    else await api.createBranch(body)
    closeBranch()
    branches.value = await api.branches()
  } catch (err) {
    branchError.value = (err as Error).message
  }
}
async function removeBranch(b: BranchRow) {
  if (!confirm(`Удалить филиал «${b.name}»?`)) return
  try {
    await api.deleteBranch(b.id)
    branches.value = await api.branches()
  } catch (err) {
    error.value = (err as Error).message
  }
}
</script>

<template>
  <div class="page narrow">
    <div class="page-head">
      <h1>Магазин</h1>
      <a class="btn btn-ghost" href="/about" target="_blank" rel="noopener"><Icon name="chevronRight" />Как это видит покупатель</a>
    </div>
    <div v-if="error" class="error-banner">{{ error }}</div>
    <div v-if="!s" class="skeleton" style="height: 500px" />

    <form v-else class="stack" @submit.prevent="save">
      <section class="card card-pad">
        <div class="card-head"><h2>О магазине</h2><span class="card-sub">страница «О нас» и «Связаться с нами»</span></div>
        <div class="grid">
          <label class="field"><span>Название</span><input v-model="s.storeName" class="input" maxlength="80" required /></label>
          <label class="field"><span>Контактный телефон</span><input v-model="s.phone" class="input" maxlength="40" placeholder="+998 78 000 00 00" /></label>
          <label class="field wide"><span>Часы работы</span><input v-model="s.workingHours" class="input" maxlength="120" placeholder="Ежедневно 08:00–22:00" /></label>
          <label class="field wide"><span>О нас</span><textarea v-model="s.aboutText" class="input area" rows="5" maxlength="4000" /></label>
        </div>
      </section>

      <section class="card card-pad">
        <div class="card-head"><h2>Доставка</h2><span class="card-sub">стоимость в оформлении заказа и страница «Условия доставки»</span></div>
        <div class="grid">
          <label class="field"><span>Стоимость доставки, сум</span><input v-model.number="s.deliveryFee" class="input" type="number" min="0" step="1000" /></label>
          <label class="field"><span>Бесплатно от, сум (пусто — никогда)</span><input v-model.number="s.freeDeliveryFrom" class="input" type="number" min="0" step="1000" /></label>
          <label class="field wide"><span>Условия доставки</span>
            <textarea v-model="s.deliveryTerms" class="input area" rows="8" maxlength="8000" />
            <small class="faint">Абзацы разделяйте пустой строкой. Строка, начинающаяся с «## », — заголовок.</small>
          </label>
        </div>
      </section>

      <section class="card card-pad">
        <div class="card-head"><h2>Возврат и обмен</h2><span class="card-sub">страница «Условия возврата и обмена»</span></div>
        <label class="field"><textarea v-model="s.returnTerms" class="input area" rows="8" maxlength="8000" aria-label="Условия возврата и обмена" /></label>
      </section>

      <section class="card card-pad">
        <div class="card-head"><h2>Заказы</h2></div>
        <label class="field narrow-field"><span>Считать заказ просроченным, если он «Новый» или «В сборке» дольше, минут</span>
          <input v-model.number="s.overdueMinutes" class="input" type="number" min="5" max="1440" />
        </label>
      </section>

      <div class="save-bar">
        <button class="btn btn-primary" :disabled="saving">Сохранить</button>
        <span v-if="saved" class="ok">✓ Сохранено — уже на сайте</span>
      </div>
    </form>

    <section class="card card-pad branches">
      <div class="card-head">
        <h2>Филиалы</h2><span class="card-sub">самовывоз, склад и доставка</span>
        <button class="btn btn-sm btn-primary" @click="openBranch()"><Icon name="plus" />Добавить</button>
      </div>
      <table class="data">
        <thead><tr><th>Филиал</th><th>Адрес</th><th>Телефон</th><th>Часы работы</th><th class="right">Заказов</th><th /></tr></thead>
        <tbody>
          <tr v-for="b in branches" :key="b.id">
            <td><b>{{ b.name }}</b></td>
            <td>{{ b.address }}</td>
            <td class="num nowrap">{{ b.phone ?? '—' }}</td>
            <td>{{ b.workingHours ?? '—' }}</td>
            <td class="right num">{{ count(b.orders) }}</td>
            <td class="right nowrap">
              <button class="btn btn-sm btn-ghost" @click="openBranch(b)">Изменить</button>
              <button class="btn btn-sm btn-ghost btn-danger" :disabled="b.orders > 0" :title="b.orders ? 'У филиала есть заказы' : ''" @click="removeBranch(b)">Удалить</button>
            </td>
          </tr>
        </tbody>
      </table>
    </section>

    <Modal v-if="editing" :title="editing.id ? 'Филиал' : 'Новый филиал'" width="560px" persistent @close="closeBranch">
      <form class="stack" @submit.prevent="saveBranch">
        <div v-if="branchError" class="error-banner">{{ branchError }}</div>
        <div class="grid">
          <label class="field"><span>Название</span><input v-model="editing.name" class="input" maxlength="60" /></label>
          <label class="field"><span>Телефон</span><input v-model="editing.phone" class="input" maxlength="40" /></label>
          <label class="field wide"><span>Адрес</span><input v-model="editing.address" class="input" maxlength="200" /></label>
          <label class="field wide"><span>Часы работы</span><input v-model="editing.workingHours" class="input" maxlength="120" /></label>
          <label v-if="!editing.id" class="field wide"><span>Ассортимент и остатки</span>
            <select v-model="editing.copyStockFrom" class="select">
              <option :value="null">Пустой — настрою в «Склад»</option>
              <option v-for="b in branches" :key="b.id" :value="b.id">Скопировать из «{{ b.name }}»</option>
            </select>
          </label>
        </div>
        <div>
          <span class="map-label">Точка на карте <span class="faint">(нажмите на карту)</span></span>
          <div ref="mapEl" class="map" />
        </div>
        <div class="row end">
          <button type="button" class="btn btn-ghost" @click="closeBranch">Отмена</button>
          <button class="btn btn-primary">Сохранить</button>
        </div>
      </form>
    </Modal>
  </div>
</template>

<style scoped>
.narrow { max-width: 980px; }
.stack { display: flex; flex-direction: column; gap: 16px; }
.grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px 16px; }
.wide { grid-column: 1 / -1; }
.area { height: auto; padding: 8px 10px; resize: vertical; line-height: 1.5; }
.narrow-field { max-width: 460px; }
.save-bar { display: flex; align-items: center; gap: 12px; position: sticky; bottom: 0; padding: 12px 0; background: linear-gradient(transparent, var(--bg) 40%); }
.ok { color: var(--good); font-weight: 600; }
.branches { margin-top: 16px; }
.branches .card-head { align-items: center; }
.row { display: flex; gap: 8px; }
.end { justify-content: flex-end; }
.map-label { display: block; font-size: 12px; font-weight: 600; color: var(--text-2); margin-bottom: 5px; }
.map { height: 260px; border-radius: 10px; border: 1px solid var(--border); }
@media (max-width: 700px) { .grid { grid-template-columns: 1fr; } }
</style>
