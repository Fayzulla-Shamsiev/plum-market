<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { api, type OrderRow, type OrdersPage, type OrderStatus } from '../api'
import Icon from '../components/Icon.vue'
import Pager from '../components/Pager.vue'
import PlatformIcon from '../components/PlatformIcon.vue'
import { deliveryLabel, dateTime, money, nextStatuses, paymentLabel, platformLabel, relative, statusLabel } from '../format'
import { useLookups } from '../store'
import AssemblySheetModal from './orders/AssemblySheetModal.vue'
import AutoReplyModal from './orders/AutoReplyModal.vue'
import ExportModal from './orders/ExportModal.vue'
import OrderDrawer from './orders/OrderDrawer.vue'

const tabs = [
  { key: 'all', label: 'Все' },
  { key: 'new', label: 'Новый' },
  { key: 'inProgress', label: 'В процессе' },
  { key: 'overdue', label: 'Просрочен' },
  { key: 'ready', label: 'Готов' },
  { key: 'onTheWay', label: 'В пути' },
  { key: 'history', label: 'История заказов' },
]

const { lookups } = useLookups()
const tab = ref('all')
const search = ref('')
const page = ref(1)
const filtersOpen = ref(false)
const filters = reactive({
  branchId: '' as number | '',
  employeeId: '' as number | '',
  payment: '',
  delivery: '',
  platform: '',
  statuses: '',
  from: '',
  to: '',
})
const activeFilterCount = computed(() => Object.values(filters).filter(v => v !== '').length)

const data = ref<OrdersPage | null>(null)
const loading = ref(false)
const error = ref('')
const openId = ref<number | null>(null)
const modal = ref<'' | 'export' | 'assembly' | 'autoreply'>('')
const toast = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    data.value = await api.orders({ tab: tab.value, search: search.value.trim(), page: page.value, pageSize: 20, ...filters })
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
watch([tab, () => ({ ...filters })], () => { page.value = 1; load() }, { deep: true })
watch(page, load)

// New orders arrive from the storefront/bot; refresh the queue periodically.
let poll = 0
onMounted(() => {
  load()
  poll = window.setInterval(() => { if (!openId.value && !modal.value) load() }, 30_000)
})
onBeforeUnmount(() => clearInterval(poll))

function resetFilters() {
  Object.assign(filters, { branchId: '', employeeId: '', payment: '', delivery: '', platform: '', statuses: '', from: '', to: '' })
}

async function quickStatus(o: OrderRow, status: OrderStatus) {
  try {
    await api.setStatus(o.id, status)
    flash(`Заказ #${o.id}: ${statusLabel[status]}. Клиенту отправлено уведомление.`)
    load()
  } catch (e) {
    flash((e as Error).message)
  }
}

async function simulate() {
  const o = await api.simulateOrder()
  flash(`Поступил новый заказ #${o.id} от ${o.customer.fullName}`)
  tab.value === 'all' || tab.value === 'new' ? load() : (tab.value = 'new')
}

let toastTimer = 0
function flash(msg: string) {
  toast.value = msg
  clearTimeout(toastTimer)
  toastTimer = window.setTimeout(() => (toast.value = ''), 3500)
}

// One-click "advance" button in the row; reopening a cancelled order stays in the drawer.
const primaryNext = (s: OrderStatus) => (s === 'Cancelled' ? undefined : nextStatuses[s].find(n => n !== 'Cancelled'))
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>Заказы</h1>
      <button class="btn btn-ghost" title="Создать тестовый заказ, как будто он пришёл из бота" @click="simulate">
        <Icon name="zap" />Тестовый заказ
      </button>
      <button class="btn" @click="modal = 'autoreply'"><Icon name="bot" />Автоответчик</button>
      <button class="btn" @click="modal = 'assembly'"><Icon name="clipboard" />Лист сборки</button>
      <button class="btn btn-primary" @click="modal = 'export'"><Icon name="download" />Экспорт</button>
    </div>

    <div class="tabs">
      <button v-for="t in tabs" :key="t.key" class="chip" :class="{ active: tab === t.key, alert: t.key === 'overdue' && (data?.counts.overdue ?? 0) > 0 }"
              @click="tab = t.key">
        {{ t.label }}<span class="count">{{ data?.counts[t.key] ?? '·' }}</span>
      </button>
    </div>

    <div class="card">
      <div class="toolbar">
        <label class="search">
          <Icon name="search" />
          <input v-model="search" class="input" placeholder="ID заказа, имя или телефон клиента" aria-label="Поиск заказов" />
        </label>
        <button class="btn" :class="{ on: filtersOpen || activeFilterCount }" @click="filtersOpen = !filtersOpen">
          <Icon name="filter" />Фильтры<span v-if="activeFilterCount" class="pill">{{ activeFilterCount }}</span>
        </button>
        <span v-if="loading" class="faint">Загрузка…</span>
      </div>

      <div v-if="filtersOpen" class="filters">
        <label class="field"><span>Филиал</span>
          <select v-model="filters.branchId" class="select">
            <option value="">Все</option>
            <option v-for="b in lookups?.branches" :key="b.id" :value="b.id">{{ b.name }}</option>
          </select>
        </label>
        <label class="field"><span>Сотрудник</span>
          <select v-model="filters.employeeId" class="select">
            <option value="">Все</option>
            <option v-for="e in lookups?.employees" :key="e.id" :value="e.id">{{ e.name }}</option>
          </select>
        </label>
        <label class="field"><span>Способ оплаты</span>
          <select v-model="filters.payment" class="select">
            <option value="">Все</option>
            <option v-for="(l, k) in paymentLabel" :key="k" :value="k">{{ l }}</option>
          </select>
        </label>
        <label class="field"><span>Тип доставки</span>
          <select v-model="filters.delivery" class="select">
            <option value="">Все</option>
            <option v-for="(l, k) in deliveryLabel" :key="k" :value="k">{{ l }}</option>
          </select>
        </label>
        <label class="field"><span>Платформа</span>
          <select v-model="filters.platform" class="select">
            <option value="">Все</option>
            <option v-for="(l, k) in platformLabel" :key="k" :value="k">{{ l }}</option>
          </select>
        </label>
        <label class="field"><span>Статус</span>
          <select v-model="filters.statuses" class="select">
            <option value="">Все</option>
            <option v-for="(l, k) in statusLabel" :key="k" :value="k">{{ l }}</option>
          </select>
        </label>
        <label class="field"><span>С даты</span><input v-model="filters.from" type="date" class="input" :max="filters.to || undefined" /></label>
        <label class="field"><span>По дату</span><input v-model="filters.to" type="date" class="input" :min="filters.from || undefined" /></label>
        <div class="field"><span>&nbsp;</span><button class="btn btn-ghost" :disabled="!activeFilterCount" @click="resetFilters">Сбросить</button></div>
      </div>

      <div v-if="error" class="error-banner" style="margin: 12px">{{ error }}</div>

      <div class="table-wrap">
        <table class="data">
          <thead>
            <tr>
              <th>ID</th><th>Клиент</th><th>Дата и время</th><th class="right">Стоимость</th><th>Оплата</th>
              <th>Доставка</th><th>Статус</th><th>Платформа</th><th class="right">Действие</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="o in data?.items" :key="o.id" class="clickable" @click="openId = o.id">
              <td class="num"><b>#{{ o.id }}</b></td>
              <td>
                <div class="nowrap"><b>{{ o.customer.fullName }}</b></div>
                <div class="faint num small">{{ o.customer.phone }}</div>
              </td>
              <td class="nowrap">
                <div class="num">{{ dateTime(o.createdAt) }}</div>
                <div class="faint small">{{ relative(o.createdAt) }}</div>
              </td>
              <td class="right num nowrap"><b>{{ money(o.total) }}</b><div class="faint small">{{ o.itemsCount }} шт</div></td>
              <td class="nowrap">{{ paymentLabel[o.paymentMethod] }}</td>
              <td class="nowrap">{{ deliveryLabel[o.deliveryType] }}<div class="faint small">{{ o.branch }}</div></td>
              <td><span class="badge" :class="o.status">{{ statusLabel[o.status] }}</span></td>
              <td><PlatformIcon :platform="o.platform" show-label /></td>
              <td class="right nowrap" @click.stop>
                <button v-if="primaryNext(o.status)" class="btn btn-sm" @click="quickStatus(o, primaryNext(o.status)!)">
                  → {{ statusLabel[primaryNext(o.status)!] }}
                </button>
              </td>
            </tr>
            <tr v-if="data && !data.items.length"><td colspan="9" class="empty">Заказов не найдено</td></tr>
            <tr v-if="!data"><td colspan="9"><div class="skeleton" style="height: 300px" /></td></tr>
          </tbody>
        </table>
      </div>
      <Pager v-if="data" v-model:page="page" :page-size="data.pageSize" :total="data.total" />
    </div>

    <OrderDrawer v-if="openId" :id="openId" @close="openId = null" @changed="load" />
    <ExportModal v-if="modal === 'export'" @close="modal = ''" />
    <AssemblySheetModal v-if="modal === 'assembly'" @close="modal = ''" />
    <AutoReplyModal v-if="modal === 'autoreply'" @close="modal = ''" @saved="flash('Тексты автоответчика сохранены')" />

    <Transition name="toast"><div v-if="toast" class="toast" role="status">{{ toast }}</div></Transition>
  </div>
</template>

<style scoped>
.tabs { display: flex; gap: 8px; flex-wrap: wrap; margin-bottom: 14px; }
.chip.alert:not(.active) { border-color: #f1b7b7; color: var(--bad); }
.chip.alert:not(.active) .count { background: var(--bad-bg); color: var(--bad); }
.toolbar { display: flex; gap: 10px; align-items: center; padding: 12px 14px; border-bottom: 1px solid var(--border); flex-wrap: wrap; }
.btn.on { border-color: var(--plum-500); color: var(--plum-600); }
.pill { background: var(--plum-600); color: #fff; border-radius: 9px; font-size: 11px; padding: 0 6px; }
.filters { display: grid; grid-template-columns: repeat(auto-fill, minmax(170px, 1fr)); gap: 12px; padding: 14px; background: var(--surface-2); border-bottom: 1px solid var(--border); }
.small { font-size: 12px; }
.toast { position: fixed; bottom: 24px; left: 50%; transform: translateX(-50%); background: #16151a; color: #fff; padding: 11px 18px; border-radius: 10px; box-shadow: var(--shadow-lg); z-index: 2000; max-width: calc(100vw - 32px); }
.toast-enter-active, .toast-leave-active { transition: all .2s; }
.toast-enter-from, .toast-leave-to { opacity: 0; transform: translate(-50%, 10px); }
</style>
