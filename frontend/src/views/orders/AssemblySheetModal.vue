<script setup lang="ts">
import { ref } from 'vue'
import { api, download, type OrderStatus } from '../../api'
import Icon from '../../components/Icon.vue'
import Modal from '../../components/Modal.vue'
import { periodFor } from '../../period'
import StatusChecklist from './StatusChecklist.vue'

const emit = defineEmits<{ close: [] }>()
const today = periodFor('today')
const from = ref(today.from)
const to = ref(today.to)
const statuses = ref<OrderStatus[]>(['New', 'InProgress', 'Overdue', 'Ready'])
const mode = ref<'orders' | 'products'>('orders')
const format = ref<'pdf' | 'xlsx'>('pdf')

function run() {
  const url = api.assemblyUrl({ from: from.value, to: to.value, statuses: statuses.value.join(','), mode: mode.value, format: format.value })
  // PDFs are handy to preview/print straight away; Excel files just download.
  if (format.value === 'pdf') window.open(url, '_blank', 'noopener')
  else download(url)
  emit('close')
}
</script>

<template>
  <Modal title="Лист сборки" @close="emit('close')">
    <div class="stack">
      <div class="field">
        <span>Период</span>
        <div class="row">
          <input v-model="from" type="date" class="input grow" :max="to" aria-label="С" />
          <span class="faint">—</span>
          <input v-model="to" type="date" class="input grow" :min="from" aria-label="По" />
        </div>
      </div>
      <StatusChecklist v-model="statuses" />
      <div class="field">
        <span>Тип выгрузки</span>
        <div class="options">
          <label class="opt" :class="{ on: mode === 'orders' }">
            <input v-model="mode" type="radio" value="orders" />
            <b>По заказам</b><small>Каждый заказ отдельным блоком с товарами и чекбоксами</small>
          </label>
          <label class="opt" :class="{ on: mode === 'products' }">
            <input v-model="mode" type="radio" value="products" />
            <b>По товарам</b><small>Сводно: сколько каждого товара собрать и для каких заказов</small>
          </label>
        </div>
      </div>
      <div class="field">
        <span>Формат файла</span>
        <div class="row">
          <label class="radio"><input v-model="format" type="radio" value="pdf" />PDF</label>
          <label class="radio"><input v-model="format" type="radio" value="xlsx" />Excel</label>
        </div>
      </div>
    </div>
    <template #footer>
      <button class="btn" @click="emit('close')">Отмена</button>
      <button class="btn btn-primary" :disabled="!statuses.length || !from || !to" @click="run">
        <Icon name="download" />Сформировать
      </button>
    </template>
  </Modal>
</template>

<style scoped>
.stack { display: flex; flex-direction: column; gap: 18px; }
.radio { display: inline-flex; gap: 6px; align-items: center; cursor: pointer; }
.radio input { accent-color: var(--plum-600); }
.options { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
.opt { border: 1px solid var(--border-strong); border-radius: 10px; padding: 10px 12px; cursor: pointer; display: flex; flex-direction: column; gap: 2px; }
.opt input { position: absolute; opacity: 0; }
.opt small { color: var(--text-2); font-size: 12px; }
.opt.on { border-color: var(--plum-500); background: var(--plum-50); box-shadow: 0 0 0 1px var(--plum-500); }
@media (max-width: 500px) { .options { grid-template-columns: 1fr; } }
</style>
