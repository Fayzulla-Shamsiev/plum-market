<script setup lang="ts">
import { ref } from 'vue'
import { api, download, type OrderStatus } from '../../api'
import Icon from '../../components/Icon.vue'
import Modal from '../../components/Modal.vue'
import { periodFor } from '../../period'
import StatusChecklist from './StatusChecklist.vue'

const emit = defineEmits<{ close: [] }>()
const scope = ref<'range' | 'all'>('range')
const initial = periodFor('month')
const from = ref(initial.from)
const to = ref(initial.to)
const statuses = ref<OrderStatus[]>(['Completed', 'Cancelled'])

function run() {
  download(api.exportUrl({
    from: scope.value === 'range' ? from.value : '',
    to: scope.value === 'range' ? to.value : '',
    statuses: statuses.value.join(','),
  }))
  emit('close')
}
</script>

<template>
  <Modal title="Экспорт заказов" @close="emit('close')">
    <div class="stack">
      <div class="field">
        <span>Период</span>
        <div class="row">
          <label class="radio"><input v-model="scope" type="radio" value="range" />Диапазон дат</label>
          <label class="radio"><input v-model="scope" type="radio" value="all" />За всё время</label>
        </div>
        <div v-if="scope === 'range'" class="row">
          <input v-model="from" type="date" class="input grow" :max="to" aria-label="С" />
          <span class="faint">—</span>
          <input v-model="to" type="date" class="input grow" :min="from" aria-label="По" />
        </div>
      </div>
      <StatusChecklist v-model="statuses" />
      <p class="faint hint">Файл Excel (.xlsx): ID, дата, клиент, статус, платформа, оплата, доставка, филиал, сотрудник, состав и суммы.</p>
    </div>
    <template #footer>
      <button class="btn" @click="emit('close')">Отмена</button>
      <button class="btn btn-primary" :disabled="!statuses.length || (scope === 'range' && (!from || !to))" @click="run">
        <Icon name="download" />Скачать Excel
      </button>
    </template>
  </Modal>
</template>

<style scoped>
.stack { display: flex; flex-direction: column; gap: 18px; }
.radio { display: inline-flex; gap: 6px; align-items: center; cursor: pointer; }
.radio input { accent-color: var(--plum-600); }
.hint { margin: 0; font-size: 12px; }
</style>
