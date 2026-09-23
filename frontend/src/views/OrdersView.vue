<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { api, type OrderRow, type OrdersPage, type OrderStatus } from '../api'
import Icon from '../components/Icon.vue'
import Pager from '../components/Pager.vue'
import { dateTime, deliveryLabel, money, relative, statusText, stepText } from '../format'
import { refreshNewOrders, useLookups } from '../store'
import AssemblySheetModal from './orders/AssemblySheetModal.vue'
import AutoReplyModal from './orders/AutoReplyModal.vue'
import ExportModal from './orders/ExportModal.vue'
import OrderDrawer from './orders/OrderDrawer.vue'

// Orders from the storefront. The store moves each one through the MVP flow step by step:
// Новый → В сборке → Готов → Передан в доставку → В пути → Доставлен → Завершён (pickup: Готов к выдаче → Завершён).
const tabs = [
  { key: 'all', label: 'Все' },
  { key: 'new', label: 'Новые' },
  { key: 'assembling', label: 'В сборке' },
  { key: 'ready', label: 'Готовы' },
  { key: 'delivery', label: 'В доставке' },
  { key: 'delivered', label: 'Доставлены' },
  { key: 'overdue', label: 'Просроченные' },
  { key: 'history', label: 'Завершённые и отменённые' },
]
// Board columns = the active part of the flow.
const columns: { status: OrderStatus; label: string; hint: string }[] = [
  { status: 'New', label: 'Новые', hint: 'Ждут подтверждения — покупатель ещё может отменить' },
  { status: 'Assembling', label: 'В сборке', hint: 'Заказ подтверждён, собирается' },
  { status: 'Ready', label: 'Готовы', hint: 'Самовывоз — ждёт покупателя; доставка — ждёт курьера' },
  { status: 'HandedToCourier', label: 'Переданы в доставку', hint: 'Курьер забрал заказ' },
  { status: 'OnTheWay', label: 'В пути', hint: 'Курьер едет к покупателю' },
  { status: 'Delivered', label: 'Доставлены', hint: 'Проверьте и подтвердите завершение' },
]

const { lookups } = useLookups()
function readView(): 'list' | 'board' {
  try { return localStorage.getItem('plum.ordersView') === 'list' ? 'list' : 'board' } catch { return 'board' }
}
const view = ref<'list' | 'board'>(readView())
watch(view, v => { try { localStorage.setItem('plum.ordersView', v) } catch { /* ignore */ } })
const tab = ref('all')
const search = ref('')
const page = ref(1)
const filters = reactive({ branchId: '' as number | '', delivery: '', from: '', to: '' })
const activeFilterCount = computed(() => Object.values(filters).filter(v => v !== '').length)

const data = ref<OrdersPage | null>(null)
const board = ref<OrderRow[]>([])
const loading = ref(false)
const error = ref('')
const openId = ref<number | null>(null)
const modal = ref<'' | 'export' | 'assembly' | 'autoreply'>('')
const toast = ref('')
const busy = ref<number | null>(null)
let lastNew = -1

async function load() {
  loading.value = true
  error.value = ''
  try {
    const common = { search: search.value.trim(), ...filters }
    if (view.value === 'board') {
      const res = await api.orders({ ...common, statuses: columns.map(c => c.status).join(','), pageSize: 200 })
      board.value = res.items
      data.value = res
    } else {
      data.value = await api.orders({ ...common, tab: tab.value, page: page.value, pageSize: 20 })
    }
    // A new storefront order came in since the last refresh.
    const n = data.value.counts.new ?? 0
    if (lastNew >= 0 && n > lastNew) flash(`Новый заказ с сайта (${n - lastNew})`)
    lastNew = n
    refreshNewOrders()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

let searchTimer = 0
watch(search, () => {
  clearTimeout(searchTimer)
  searchTimer = window.setTimeout(() => { page.value = 1; load() }, 300)
})
watch([tab, view, () => ({ ...filters })], () => { page.value = 1; load() }, { deep: true })
watch(page, load)

// New orders arrive from the storefront; keep the queue fresh.
let poll = 0
onMounted(() => {
  load()
  poll = window.setInterval(() => { if (!openId.value && !modal.value && document.visibilityState === 'visible') load() }, 15_000)
})
onBeforeUnmount(() => clearInterval(poll))

function resetFilters() {
  Object.assign(filters, { branchId: '', delivery: '', from: '', to: '' })
}

async function advance(o: OrderRow) {
  if (!o.next) return
  busy.value = o.id
  try {
    await api.setStatus(o.id, o.next)
    flash(`Заказ #${o.id}: ${statusText(o.next, o.deliveryType)}. Покупатель увидит это в «Мои заказы».`)
    await load()
  } catch (e) {
    flash((e as Error).message)
  } finally {
    busy.value = null
  }
}

let toastTimer = 0
function flash(msg: string) {
  toast.value = msg
  clearTimeout(toastTimer)
  toastTimer = window.setTimeout(() => (toast.value = ''), 3500)
}

const byStatus = (s: OrderStatus) => board.value.filter(o => o.status === s).sort((a, b) => a.createdAt.localeCompare(b.createdAt))
const who = (o: OrderRow) => o.recipient?.name ?? o.customer.fullName
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>Заказы</h1>
      <div class="seg" role="tablist">
        <button role="tab" :aria-selected="view === 'board'" :class="{ on: view === 'board' }" @click="view = 'board'"><Icon name="dashboard" />Доска</button>
        <button role="tab" :aria-selected="view === 'list'" :class="{ on: view === 'list' }" @click="view = 'list'"><Icon name="orders" />Список</button>
      </div>
      <button class="btn" @click="modal = 'autoreply'"><Icon name="bot" />Сообщения покупателю</button>
      <button class="btn" @click="modal = 'assembly'"><Icon name="clipboard" />Лист сборки</button>
      <button class="btn btn-primary" @click="modal = 'export'"><Icon name="download" />Экспорт</button>
    </div>

    <div class="card toolbar-card">
      <div class="toolbar">
        <label class="search">
          <Icon name="search" />
          <input v-model="search" class="input" placeholder="ID заказа, имя или телефон" aria-label="Поиск заказов" />
        </label>
        <select v-model="filters.branchId" class="select" aria-label="Филиал">
          <option value="">Все филиалы</option>
          <option v-for="b in lookups?.branches" :key="b.id" :value="b.id">{{ b.name }}</option>
        </select>
        <select v-model="filters.delivery" class="select" aria-label="Способ получения">
          <option value="">Доставка и самовывоз</option>
          <option v-for="(l, k) in deliveryLabel" :key="k" :value="k">{{ l }}</option>
        </select>
        <template v-if="view === 'list'">
          <input v-model="filters.from" type="date" class="input date" aria-label="С даты" :max="filters.to || undefined" />
          <input v-model="filters.to" type="date" class="input date" aria-label="По дату" :min="filters.from || undefined" />
        </template>
        <button v-if="activeFilterCount" class="btn btn-ghost" @click="resetFilters">Сбросить</button>
        <span v-if="loading" class="faint">Обновление…</span>
        <button v-if="data?.counts.overdue" type="button" class="overdue-note" @click="view = 'list'; tab = 'overdue'">
          ⚠ Просрочено: {{ data.counts.overdue }}
        </button>
      </div>
    </div>

    <div v-if="error" class="error-banner">{{ error }}</div>

    <!-- Board: delivery control, one column per step -->
    <div v-if="view === 'board'" class="board">
      <section v-for="c in columns" :key="c.status" class="col">
        <header :title="c.hint">
          <span class="badge" :class="c.status">{{ c.label }}</span>
          <span class="n">{{ byStatus(c.status).length }}</span>
        </header>
        <p class="hint">{{ c.hint }}</p>
        <article v-for="o in byStatus(c.status)" :key="o.id" class="ocard" :class="{ late: o.overdue }" @click="openId = o.id">
          <div class="row1">
            <b>#{{ o.id }}</b>
            <span class="faint small">{{ relative(o.createdAt) }}</span>
            <span v-if="o.overdue" class="late-tag">просрочен</span>
          </div>
          <div class="who">{{ who(o) }}</div>
          <div class="faint small">{{ o.deliveryType === 'Pickup' ? `Самовывоз · ${o.branch}` : o.address }}</div>
          <div class="row2">
            <b class="num">{{ money(o.total) }}</b>
            <span class="faint small">{{ o.itemsCount }} шт</span>
          </div>
          <button v-if="o.next" class="btn btn-sm next" :class="{ final: o.next === 'Completed' }" :disabled="busy === o.id" @click.stop="advance(o)">
            {{ stepText(o.next, o.deliveryType) }} →
          </button>
        </article>
        <div v-if="data && !byStatus(c.status).length" class="empty-col">Пусто</div>
      </section>
    </div>

    <!-- List -->
    <template v-else>
      <div class="tabs">
        <button v-for="t in tabs" :key="t.key" class="chip" :class="{ active: tab === t.key, alert: t.key === 'overdue' && (data?.counts.overdue ?? 0) > 0 }"
                @click="tab = t.key">
          {{ t.label }}<span class="count">{{ data?.counts[t.key] ?? '·' }}</span>
        </button>
      </div>
      <div class="card">
        <div class="table-wrap">
          <table class="data">
            <thead>
              <tr>
                <th>ID</th><th>Покупатель</th><th>Дата и время</th><th class="right">Сумма</th>
                <th>Получение</th><th>Статус</th><th class="right">Следующий шаг</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="o in data?.items" :key="o.id" class="clickable" @click="openId = o.id">
                <td class="num"><b>#{{ o.id }}</b></td>
                <td>
                  <div class="nowrap"><b>{{ who(o) }}</b></div>
                  <div class="faint num small">{{ o.recipient?.phone ?? o.customer.phone }}</div>
                </td>
                <td class="nowrap">
                  <div class="num">{{ dateTime(o.createdAt) }}</div>
                  <div class="faint small">{{ relative(o.createdAt) }}</div>
                </td>
                <td class="right num nowrap"><b>{{ money(o.total) }}</b><div class="faint small">{{ o.itemsCount }} шт</div></td>
                <td class="nowrap">{{ deliveryLabel[o.deliveryType] }}<div class="faint small">{{ o.branch }}</div></td>
                <td>
                  <span class="badge" :class="o.status">{{ statusText(o.status, o.deliveryType) }}</span>
                  <span v-if="o.overdue" class="badge Overdue">просрочен</span>
                </td>
                <td class="right nowrap" @click.stop>
                  <button v-if="o.next" class="btn btn-sm" :disabled="busy === o.id" @click="advance(o)">{{ stepText(o.next, o.deliveryType) }} →</button>
                </td>
              </tr>
              <tr v-if="data && !data.items.length"><td colspan="7" class="empty">Заказов не найдено</td></tr>
              <tr v-if="!data"><td colspan="7"><div class="skeleton" style="height: 300px" /></td></tr>
            </tbody>
          </table>
        </div>
        <Pager v-if="data" v-model:page="page" :page-size="data.pageSize" :total="data.total" />
      </div>
    </template>

    <OrderDrawer v-if="openId" :id="openId" @close="openId = null" @changed="load" />
    <ExportModal v-if="modal === 'export'" @close="modal = ''" />
    <AssemblySheetModal v-if="modal === 'assembly'" @close="modal = ''" />
    <AutoReplyModal v-if="modal === 'autoreply'" @close="modal = ''" @saved="flash('Тексты сообщений сохранены')" />

    <Transition name="toast"><div v-if="toast" class="toast" role="status">{{ toast }}</div></Transition>
  </div>
</template>

<style scoped>
.seg { display: inline-flex; background: var(--surface); border: 1px solid var(--border); border-radius: 9px; padding: 3px; }
.seg button { display: inline-flex; align-items: center; gap: 6px; border: 0; background: none; padding: 5px 12px; border-radius: 7px; cursor: pointer; font: inherit; font-weight: 500; color: var(--text-2); }
.seg button svg { width: 15px; height: 15px; }
.seg button.on { background: var(--plum-600); color: #fff; }
.toolbar-card { margin-bottom: 14px; }
.toolbar { display: flex; gap: 10px; align-items: center; padding: 12px 14px; flex-wrap: wrap; }
.toolbar .select { width: auto; }
.date { width: 150px; }
.overdue-note { margin-left: auto; color: var(--bad); font-weight: 600; cursor: pointer; border: 0; background: none; font: inherit; }
.tabs { display: flex; gap: 8px; flex-wrap: wrap; margin-bottom: 14px; }
.chip.alert:not(.active) { border-color: #f1b7b7; color: var(--bad); }
.chip.alert:not(.active) .count { background: var(--bad-bg); color: var(--bad); }
.small { font-size: 12px; }

.board { display: grid; grid-template-columns: repeat(6, minmax(210px, 1fr)); gap: 12px; overflow-x: auto; padding-bottom: 8px; align-items: start; }
.col { background: #efeef3; border-radius: 12px; padding: 10px; display: flex; flex-direction: column; gap: 8px; min-height: 200px; }
.col header { display: flex; align-items: center; justify-content: space-between; }
.col header .n { font-weight: 700; color: var(--text-2); }
.hint { margin: -2px 0 2px; font-size: 11.5px; color: var(--text-3); line-height: 1.35; }
.ocard { background: var(--surface); border: 1px solid var(--border); border-radius: 10px; padding: 10px; display: flex; flex-direction: column; gap: 3px; cursor: pointer; box-shadow: var(--shadow); transition: border-color .15s; }
.ocard:hover { border-color: var(--plum-500); }
.ocard.late { border-color: #f1b7b7; background: #fff8f8; }
.row1 { display: flex; align-items: center; gap: 6px; }
.late-tag { margin-left: auto; font-size: 11px; font-weight: 700; color: var(--bad); }
.who { font-weight: 600; font-size: 13.5px; }
.row2 { display: flex; justify-content: space-between; align-items: baseline; margin-top: 4px; }
.next { margin-top: 6px; width: 100%; justify-content: center; height: auto; min-height: 28px; padding: 4px 8px; white-space: normal; line-height: 1.25; }
.next.final { background: var(--good); border-color: var(--good); color: #fff; }
.empty-col { text-align: center; color: var(--text-3); font-size: 13px; padding: 16px 0; }

.toast { position: fixed; bottom: 24px; left: 50%; transform: translateX(-50%); background: #16151a; color: #fff; padding: 11px 18px; border-radius: 10px; box-shadow: var(--shadow-lg); z-index: 2000; max-width: calc(100vw - 32px); }
.toast-enter-active, .toast-leave-active { transition: all .2s; }
.toast-enter-from, .toast-leave-to { opacity: 0; transform: translate(-50%, 10px); }
</style>
