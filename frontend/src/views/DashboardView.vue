<script setup lang="ts">
import {
  BarElement, CategoryScale, Chart as ChartJS, Filler, Legend, LinearScale, LineElement, PointElement, Tooltip,
  type ChartOptions,
} from 'chart.js'
import { computed, ref, watch } from 'vue'
import { Bar, Line } from 'vue-chartjs'
import { api, type Dashboard } from '../api'
import Icon from '../components/Icon.vue'
import KpiCard from '../components/KpiCard.vue'
import OrdersMap from '../components/OrdersMap.vue'
import PeriodPicker from '../components/PeriodPicker.vue'
import PlatformIcon from '../components/PlatformIcon.vue'
import { compactMoney, count, money, platformColor, platformLabel, series } from '../format'
import { periodFor, type Period } from '../period'
import { useLookups } from '../store'

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, BarElement, Filler, Tooltip, Legend)
ChartJS.defaults.font.family = getComputedStyle(document.documentElement).fontFamily
ChartJS.defaults.color = '#55535e'

const { lookups } = useLookups()
const period = ref<Period>(periodFor('month'))
const branchId = ref<number | ''>('')
const data = ref<Dashboard | null>(null)
const loading = ref(false)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    data.value = await api.dashboard({ from: period.value.from, to: period.value.to, branchId: branchId.value })
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
}
watch([period, branchId], load, { immediate: true })

const granularityLabel = computed(() =>
  ({ hour: 'по часам', day: 'по дням', month: 'по месяцам' })[data.value?.period.granularity ?? 'day'])

// ---- shared chart styling: recessive grid, one y-axis, index tooltip (crosshair behaviour) ----
const grid = { color: '#efeef2' }
const baseOptions = {
  responsive: true,
  maintainAspectRatio: false,
  interaction: { mode: 'index' as const, intersect: false },
  plugins: {
    legend: { position: 'top' as const, align: 'end' as const, labels: { boxWidth: 10, boxHeight: 10, useBorderRadius: true, borderRadius: 2 } },
    tooltip: { backgroundColor: '#16151a', padding: 10, cornerRadius: 8, boxPadding: 4 },
  },
}

const revenueData = computed(() => ({
  labels: data.value?.revenueChart.map(p => p.label) ?? [],
  datasets: [
    {
      label: 'Доход', data: data.value?.revenueChart.map(p => p.revenue) ?? [],
      borderColor: series[0], backgroundColor: 'rgba(42,120,214,0.10)', fill: true,
      borderWidth: 2, pointRadius: 0, pointHoverRadius: 5, tension: 0.3, cubicInterpolationMode: 'monotone' as const,
    },
    {
      label: 'Прибыль', data: data.value?.revenueChart.map(p => p.profit) ?? [],
      borderColor: series[1], backgroundColor: series[1], borderWidth: 2, pointRadius: 0, pointHoverRadius: 5, tension: 0.3, cubicInterpolationMode: 'monotone' as const,
    },
  ],
}))
const revenueOptions: ChartOptions<'line'> = {
  ...baseOptions,
  scales: {
    x: { grid: { display: false }, ticks: { maxTicksLimit: 12, autoSkip: true } },
    y: { grid, border: { display: false }, ticks: { callback: v => compactMoney(Number(v)) } },
  },
  plugins: {
    ...baseOptions.plugins,
    tooltip: { ...baseOptions.plugins.tooltip, callbacks: { label: c => ` ${c.dataset.label}: ${money(Number(c.raw))}` } },
  },
}

const dynamicsData = computed(() => ({
  labels: data.value?.ordersDynamics.map(p => p.label) ?? [],
  datasets: [
    { label: 'Новые / в работе', data: data.value?.ordersDynamics.map(p => p.new) ?? [], backgroundColor: series[0] },
    { label: 'Выполненные', data: data.value?.ordersDynamics.map(p => p.completed) ?? [], backgroundColor: series[2] },
    { label: 'Отменённые', data: data.value?.ordersDynamics.map(p => p.cancelled) ?? [], backgroundColor: series[1] },
  ].map(d => ({ ...d, borderColor: '#fff', borderWidth: { top: 2 }, borderRadius: 3, borderSkipped: 'start' as const, maxBarThickness: 22 })),
}))
const dynamicsOptions: ChartOptions<'bar'> = {
  ...baseOptions,
  scales: {
    x: { stacked: true, grid: { display: false }, ticks: { maxTicksLimit: 12 } },
    y: { stacked: true, grid, border: { display: false }, ticks: { precision: 0 } },
  },
}

const platformMax = computed(() => Math.max(1, ...(data.value?.byPlatform.map(p => p.orders) ?? [1])))
const sourceMax = computed(() => Math.max(1, ...(data.value?.trafficSources.map(s => s.users) ?? [1])))
const sourceTotal = computed(() => data.value?.trafficSources.reduce((s, x) => s + x.users, 0) ?? 0)
const productMax = computed(() => Math.max(1, ...(data.value?.topProducts.map(p => p.quantity) ?? [1])))
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>Дашборд</h1>
      <select v-model="branchId" class="select" aria-label="Филиал">
        <option value="">Все филиалы</option>
        <option v-for="b in lookups?.branches" :key="b.id" :value="b.id">{{ b.name }}</option>
      </select>
      <PeriodPicker v-model="period" />
    </div>

    <div v-if="error" class="error-banner">Не удалось загрузить данные: {{ error }}</div>

    <div v-if="!data" class="grid-kpi">
      <div v-for="i in 3" :key="i" class="skeleton" style="height: 190px" />
    </div>

    <template v-else>
      <div class="grid-kpi" :class="{ dim: loading }">
        <KpiCard title="Доход" icon="tariff" :value="money(data.revenue.current.revenue)"
                 :current="data.revenue.current.revenue" :previous="data.revenue.previous.revenue"
                 :lines="[
                   { label: 'Себестоимость', value: money(data.revenue.current.cost) },
                   { label: 'Доставка', value: money(data.revenue.current.delivery) },
                   { label: 'Прибыль', value: money(data.revenue.current.profit) },
                 ]" />
        <KpiCard title="Заказы" icon="orders" :value="count(data.orders.current.total)"
                 :current="data.orders.current.total" :previous="data.orders.previous.total"
                 :lines="[
                   { label: 'Новые / в работе', value: count(data.orders.current.new), color: series[0] },
                   { label: 'Выполненные', value: count(data.orders.current.completed), color: series[2] },
                   { label: 'Отменённые', value: count(data.orders.current.cancelled), color: series[1] },
                 ]" />
        <KpiCard title="Клиенты" icon="customers" :value="count(data.customers.current.total)"
                 :current="data.customers.current.total" :previous="data.customers.previous.total"
                 :lines="[
                   { label: 'Новые', value: count(data.customers.current.new) },
                   { label: 'Вернувшиеся', value: count(data.customers.current.returning) },
                   { label: 'Средний чек', value: money(data.customers.current.averageOrder) },
                 ]" />
      </div>

      <div class="grid-2" :class="{ dim: loading }">
        <section class="card card-pad">
          <div class="card-head">
            <h2>Статистика по доходу</h2><span class="card-sub">выполненные заказы, {{ granularityLabel }}</span>
          </div>
          <div class="chart"><Line :data="revenueData" :options="revenueOptions" /></div>
        </section>
        <section class="card card-pad">
          <div class="card-head"><h2>Статистика заказов</h2><span class="card-sub">по каналам</span></div>
          <ul class="bars">
            <li v-for="p in data.byPlatform" :key="p.platform">
              <div class="bar-label"><PlatformIcon :platform="p.platform" show-label /></div>
              <div class="bar-track">
                <div class="bar-fill" :style="{ width: (p.orders / platformMax) * 100 + '%', background: platformColor[p.platform] }" />
              </div>
              <div class="bar-value num">{{ count(p.orders) }}</div>
              <div class="bar-sub num faint">{{ money(p.revenue) }}</div>
            </li>
          </ul>
          <p class="note faint">
            {{ platformLabel.Telegram }}-бот — {{ Math.round((data.byPlatform[0].orders / Math.max(1, data.orders.current.total)) * 100) }}% всех заказов за период
          </p>
        </section>
      </div>

      <div class="grid-2" :class="{ dim: loading }">
        <section class="card card-pad">
          <div class="card-head"><h2>Динамика заказов</h2><span class="card-sub">по статусам, {{ granularityLabel }}</span></div>
          <div class="chart"><Bar :data="dynamicsData" :options="dynamicsOptions" /></div>
        </section>
        <section class="card card-pad">
          <div class="card-head">
            <h2>Источники трафика</h2><span class="card-sub num">{{ count(sourceTotal) }} польз.</span>
          </div>
          <ul class="bars">
            <li v-for="s in data.trafficSources" :key="s.source">
              <div class="bar-label">{{ s.source }}</div>
              <div class="bar-track"><div class="bar-fill" :style="{ width: (s.users / sourceMax) * 100 + '%' }" /></div>
              <div class="bar-value num">{{ count(s.users) }}</div>
              <div class="bar-sub num faint">{{ Math.round((s.users / Math.max(1, sourceTotal)) * 100) }}%</div>
            </li>
          </ul>
        </section>
      </div>

      <div class="grid-2" :class="{ dim: loading }">
        <section class="card card-pad">
          <div class="card-head"><h2>Заказы на карте</h2><span class="card-sub">доставка, последние {{ data.map.orders.length }}</span></div>
          <OrdersMap :data="data.map" />
        </section>
        <section class="card card-pad">
          <div class="card-head"><h2>Топ-10 продуктов</h2><span class="card-sub">по количеству</span></div>
          <ol class="top">
            <li v-for="(p, i) in data.topProducts" :key="p.name">
              <span class="rank num">{{ i + 1 }}</span>
              <div class="grow">
                <div class="top-name">{{ p.name }}</div>
                <div class="bar-track thin"><div class="bar-fill" :style="{ width: (p.quantity / productMax) * 100 + '%' }" /></div>
              </div>
              <div class="right">
                <div class="num"><b>{{ count(p.quantity) }}</b> шт</div>
                <div class="num faint small">{{ money(p.revenue) }}</div>
              </div>
            </li>
            <li v-if="!data.topProducts.length" class="empty">Нет продаж за период</li>
          </ol>
        </section>
      </div>

      <section class="card" :class="{ dim: loading }">
        <div class="card-head card-pad" style="margin: 0; padding-bottom: 0"><h2>Топ-10 клиентов</h2></div>
        <div class="table-wrap">
          <table class="data">
            <thead><tr><th>#</th><th>Клиент</th><th>Телефон</th><th class="right">Заказов</th><th class="right">Сумма</th><th></th></tr></thead>
            <tbody>
              <tr v-for="(c, i) in data.topCustomers" :key="c.id">
                <td class="faint num">{{ i + 1 }}</td>
                <td><b>{{ c.name }}</b></td>
                <td class="num">{{ c.phone }}</td>
                <td class="right num">{{ c.orders }}</td>
                <td class="right num">{{ money(c.total) }}</td>
                <td class="right">
                  <RouterLink class="btn btn-sm btn-ghost" :to="{ path: '/customers', query: { open: c.id } }">
                    Профиль <Icon name="chevronRight" />
                  </RouterLink>
                </td>
              </tr>
              <tr v-if="!data.topCustomers.length"><td colspan="6" class="empty">Нет заказов за период</td></tr>
            </tbody>
          </table>
        </div>
      </section>
    </template>
  </div>
</template>

<style scoped>
.grid-kpi { display: grid; grid-template-columns: repeat(3, 1fr); gap: 16px; margin-bottom: 16px; }
.grid-2 { display: grid; grid-template-columns: minmax(0, 2fr) minmax(0, 1fr); gap: 16px; margin-bottom: 16px; }
.dim { opacity: .55; transition: opacity .15s; }
.chart { height: 300px; }
.bars { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 14px; }
.bars li { display: grid; grid-template-columns: 1fr auto; grid-template-rows: auto auto; gap: 4px 10px; align-items: center; }
.bar-label { grid-column: 1; font-weight: 550; }
.bar-value { grid-column: 2; grid-row: 1; text-align: right; font-weight: 650; }
.bar-track { grid-column: 1; height: 10px; background: #f1f0f4; border-radius: 5px; overflow: hidden; }
.bar-track.thin { height: 6px; margin-top: 4px; }
.bar-fill { height: 100%; background: var(--plum-500); border-radius: 0 4px 4px 0; min-width: 2px; }
.bar-sub { grid-column: 2; grid-row: 2; text-align: right; font-size: 12px; }
.note { margin: 16px 0 0; font-size: 12px; }
.top { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 10px; }
.top li { display: flex; align-items: center; gap: 10px; }
.rank { width: 22px; height: 22px; border-radius: 6px; background: var(--plum-50); color: var(--plum-600); font-size: 12px; font-weight: 700; display: grid; place-items: center; flex: none; }
.top-name { font-weight: 550; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.top .right { text-align: right; white-space: nowrap; }
.small { font-size: 12px; }

@media (max-width: 1100px) {
  .grid-2 { grid-template-columns: minmax(0, 1fr); }
}
@media (max-width: 760px) {
  .grid-kpi { grid-template-columns: 1fr; }
}
</style>
