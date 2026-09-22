<script setup lang="ts">
import { BarElement, CategoryScale, Chart as ChartJS, LinearScale, Tooltip, type ChartOptions } from 'chart.js'
import { computed, onMounted, ref } from 'vue'
import { Bar } from 'vue-chartjs'
import { catalogApi, type SalesHistory, type StockRow } from '../../api'
import Modal from '../../components/Modal.vue'
import { count, date, loc, money, series } from '../../format'
import { useLookups } from '../../store'

ChartJS.register(CategoryScale, LinearScale, BarElement, Tooltip)

const props = defineProps<{ product: StockRow; branchId: number | '' }>()
const emit = defineEmits<{ close: [] }>()
const { lookups } = useLookups()
const h = ref<SalesHistory | null>(null)
const branchName = computed(() => lookups.value?.branches.find(b => b.id === props.branchId)?.name ?? 'все филиалы')

onMounted(async () => { h.value = await catalogApi.salesHistory(props.product.id, props.branchId) })

// One series (units sold per month) → no legend; the card title names it.
const chartData = computed(() => ({
  labels: h.value?.months.map(m => m.month) ?? [],
  datasets: [{
    label: 'Продано', data: h.value?.months.map(m => m.quantity) ?? [], backgroundColor: series[0],
    borderRadius: 4, borderSkipped: 'start' as const, maxBarThickness: 26,
  }],
}))
const options = computed<ChartOptions<'bar'>>(() => ({
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: { display: false },
    tooltip: {
      backgroundColor: '#16151a', padding: 10, cornerRadius: 8,
      callbacks: { label: c => ` ${count(Number(c.raw))} ${props.product.unit} · ${money(h.value!.months[c.dataIndex].revenue)}` },
    },
  },
  scales: {
    x: { grid: { display: false } },
    y: { grid: { color: '#efeef2' }, border: { display: false }, ticks: { precision: 0 } },
  },
}))
</script>

<template>
  <Modal :title="`История продаж`" side width="520px" @close="emit('close')">
    <div class="head">
      <b>{{ loc(product.name) }}</b>
      <span class="faint small">Выполненные заказы · {{ branchName }} · последние 12 месяцев</span>
    </div>
    <div v-if="!h" class="skeleton" style="height: 320px" />
    <template v-else>
      <div class="tiles">
        <div><span>Всего продано</span><b class="num">{{ count(h.totalQuantity) }} {{ product.unit }}</b></div>
        <div><span>Общая сумма</span><b class="num">{{ money(h.totalRevenue) }}</b></div>
      </div>
      <p v-if="h.firstSaleAt" class="faint small">Первая продажа за период: {{ date(h.firstSaleAt) }}</p>
      <h3>Продажи по месяцам, {{ product.unit }}</h3>
      <div class="chart"><Bar :data="chartData" :options="options" /></div>
      <table class="data months">
        <thead><tr><th>Месяц</th><th class="right">Кол-во</th><th class="right">Сумма</th></tr></thead>
        <tbody>
          <tr v-for="m in [...h.months].reverse()" :key="m.month">
            <td class="num">{{ m.month }}</td><td class="right num">{{ count(m.quantity) }}</td><td class="right num">{{ money(m.revenue) }}</td>
          </tr>
        </tbody>
      </table>
    </template>
  </Modal>
</template>

<style scoped>
.head { display: flex; flex-direction: column; gap: 2px; margin-bottom: 14px; }
.tiles { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; margin-bottom: 8px; }
.tiles div { border: 1px solid var(--border); border-radius: 10px; padding: 10px 12px; display: flex; flex-direction: column; }
.tiles span { font-size: 12px; color: var(--text-3); }
.tiles b { font-size: 18px; }
h3 { margin: 14px 0 8px; }
.chart { height: 220px; }
.months { margin-top: 14px; font-size: 13px; }
.months td, .months th { padding: 6px 8px; }
.small { font-size: 12px; }
</style>
