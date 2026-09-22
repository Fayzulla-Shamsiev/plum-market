<script setup lang="ts">
import { ref, watch } from 'vue'
import { catalogApi, type StockRow, type StockStatus } from '../../api'
import BranchSwitcher from '../../components/BranchSwitcher.vue'
import Icon from '../../components/Icon.vue'
import ProductThumb from '../../components/ProductThumb.vue'
import { dateTime, loc, stockStatusLabel } from '../../format'
import { catalogBranch } from '../../store'
import SalesHistoryPanel from './SalesHistoryPanel.vue'

type Field = 'costPrice' | 'price' | 'weightGrams' | 'quantity'

const rows = ref<StockRow[] | null>(null)
const velocityDays = ref(30)
const search = ref('')
const status = ref<StockStatus | ''>('')
const historyFor = ref<StockRow | null>(null)
const flash = ref<Record<string, 'ok' | 'err'>>({})
const error = ref('')

async function load() {
  if (catalogBranch.value === '') return
  const r = await catalogApi.stock({ branchId: catalogBranch.value, search: search.value.trim(), status: status.value })
  rows.value = r.items
  velocityDays.value = r.velocityDays
}
let timer = 0
watch(search, () => { clearTimeout(timer); timer = window.setTimeout(load, 300) })
watch([status, catalogBranch], load, { immediate: true })

/** Saves one edited cell; flashes the cell green/red. */
async function patch(r: StockRow, field: Field | 'status', raw: string) {
  if (catalogBranch.value === '') return
  const value = field === 'status' ? raw : raw === '' ? null : Number(raw)
  if (field !== 'status' && value !== null && Number.isNaN(value)) return
  if ((r as unknown as Record<string, unknown>)[field] === value) return
  const key = `${r.id}:${field}`
  error.value = ''
  try {
    const res = await catalogApi.patchStock(r.id, catalogBranch.value, { [field]: value })
    Object.assign(r, res)
    flash.value[key] = 'ok'
  } catch (e) {
    flash.value[key] = 'err'
    error.value = `${loc(r.name)}: ${(e as Error).message}`
  }
  setTimeout(() => delete flash.value[key], 1200)
}

const onInput = (r: StockRow, f: Field) => (ev: Event) => patch(r, f, (ev.target as HTMLInputElement).value)
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>Склад</h1>
      <BranchSwitcher require-branch />
    </div>
    <div v-if="error" class="error-banner">{{ error }}</div>
    <div class="card">
      <div class="toolbar">
        <label class="search">
          <Icon name="search" />
          <input v-model="search" class="input" placeholder="Найти товар" aria-label="Поиск" />
        </label>
        <select v-model="status" class="select" aria-label="Статус наличия">
          <option value="">Любое наличие</option>
          <option v-for="(l, k) in stockStatusLabel" :key="k" :value="k">{{ l }}</option>
        </select>
        <span class="faint small hint">Значения сохраняются сразу после изменения. Цены и масса — общие для всех филиалов, наличие — для выбранного.</span>
      </div>
      <div class="table-wrap">
        <table class="data">
          <thead>
            <tr>
              <th>Товар</th><th>Входная цена</th><th>Масса, г</th><th>Цена продажи</th><th class="right">Маржа</th>
              <th>Наличие</th><th>Остаток</th><th>Обновлено</th><th class="right" :title="`Продано в день за последние ${velocityDays} дней`">Скорость продаж</th><th></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="r in rows" :key="r.id" :class="{ inactive: !r.isActive }">
              <td>
                <div class="prod">
                  <ProductThumb :url="r.imageUrl" :name="loc(r.name)" :size="32" />
                  <span class="pname">{{ loc(r.name) }}</span>
                </div>
              </td>
              <td><input class="input cell" :class="flash[`${r.id}:costPrice`]" type="number" min="0" :value="r.costPrice" aria-label="Входная цена" @change="onInput(r, 'costPrice')($event)" /></td>
              <td><input class="input cell sm" :class="flash[`${r.id}:weightGrams`]" type="number" min="0" :value="r.weightGrams ?? ''" aria-label="Масса" @change="onInput(r, 'weightGrams')($event)" /></td>
              <td><input class="input cell" :class="flash[`${r.id}:price`]" type="number" min="1" :value="r.price" aria-label="Цена продажи" @change="onInput(r, 'price')($event)" /></td>
              <td class="right num"><b :class="{ low: r.marginPercent < 30 }">{{ r.marginPercent.toFixed(1) }}%</b></td>
              <td>
                <select class="select cell-sel" :class="[r.status, flash[`${r.id}:status`]]" :value="r.status" aria-label="Наличие"
                        @change="patch(r, 'status', ($event.target as HTMLSelectElement).value)">
                  <option v-for="(l, k) in stockStatusLabel" :key="k" :value="k">{{ l }}</option>
                </select>
              </td>
              <td>
                <input v-if="r.status !== 'Unlimited'" class="input cell sm" :class="flash[`${r.id}:quantity`]" type="number" min="0" :value="r.quantity"
                       aria-label="Остаток" @change="onInput(r, 'quantity')($event)" />
                <span v-else class="faint">∞</span>
              </td>
              <td class="num small nowrap">{{ dateTime(r.updatedAt) }}</td>
              <td class="right num nowrap">{{ r.velocity.toLocaleString('ru-RU') }}<span class="faint small"> {{ r.unit }}/день</span></td>
              <td class="right">
                <button class="btn btn-sm btn-ghost btn-icon" title="История продаж" aria-label="История продаж" @click="historyFor = r"><Icon name="history" /></button>
              </td>
            </tr>
            <tr v-if="rows && !rows.length"><td colspan="10" class="empty">В этом филиале нет товаров</td></tr>
            <tr v-if="!rows"><td colspan="10"><div class="skeleton" style="height: 300px" /></td></tr>
          </tbody>
        </table>
      </div>
    </div>
    <SalesHistoryPanel v-if="historyFor" :product="historyFor" :branch-id="catalogBranch" @close="historyFor = null" />
  </div>
</template>

<style scoped>
.toolbar { display: flex; gap: 10px; align-items: center; padding: 12px 14px; border-bottom: 1px solid var(--border); flex-wrap: wrap; }
.hint { margin-left: auto; max-width: 420px; }
.prod { display: flex; align-items: center; gap: 10px; min-width: 170px; }
.pname { font-weight: 600; }
.cell { width: 96px; height: 30px; font-variant-numeric: tabular-nums; transition: background .3s, border-color .3s; }
.cell.sm { width: 72px; }
.cell-sel { height: 30px; font-size: 13px; font-weight: 600; transition: background .3s; }
.cell-sel.Unlimited { color: var(--good); }
.cell-sel.Limited { color: var(--warn); }
.cell-sel.OutOfStock { color: var(--bad); }
.ok { background: var(--good-bg) !important; border-color: var(--good) !important; }
.err { background: var(--bad-bg) !important; border-color: var(--bad) !important; }
.low { color: var(--warn); }
tr.inactive .pname { opacity: .5; }
.small { font-size: 12px; }
</style>
