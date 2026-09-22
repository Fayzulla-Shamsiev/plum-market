<script setup lang="ts">
import { ref, watch } from 'vue'
import { periodFor, type Period } from '../period'

const props = defineProps<{ modelValue: Period }>()
const emit = defineEmits<{ 'update:modelValue': [Period] }>()

const presets = [
  { key: 'today', label: 'Сегодня' },
  { key: 'week', label: 'Неделя' },
  { key: 'month', label: 'Месяц' },
  { key: 'quarter', label: 'Квартал' },
  { key: 'year', label: 'Год' },
  { key: 'custom', label: 'Период' },
]

const from = ref(props.modelValue.from)
const to = ref(props.modelValue.to)
watch(() => props.modelValue, v => { from.value = v.from; to.value = v.to })

function pick(key: string) {
  if (key === 'custom') emit('update:modelValue', { preset: 'custom', from: from.value, to: to.value })
  else emit('update:modelValue', periodFor(key))
}
function applyCustom() {
  if (from.value && to.value) emit('update:modelValue', { preset: 'custom', from: from.value, to: to.value })
}
</script>

<template>
  <div class="period">
    <div class="seg" role="group" aria-label="Период">
      <button v-for="p in presets" :key="p.key" :class="{ active: modelValue.preset === p.key }" @click="pick(p.key)">
        {{ p.label }}
      </button>
    </div>
    <div v-if="modelValue.preset === 'custom'" class="custom">
      <input v-model="from" type="date" class="input" :max="to" @change="applyCustom" />
      <span class="faint">—</span>
      <input v-model="to" type="date" class="input" :min="from" @change="applyCustom" />
    </div>
  </div>
</template>

<style scoped>
.period { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
.seg { display: inline-flex; max-width: 100%; overflow-x: auto; scrollbar-width: none; background: var(--surface); border: 1px solid var(--border-strong); border-radius: 9px; padding: 2px; }
.seg button { flex: none; border: 0; background: transparent; font: inherit; padding: 5px 11px; border-radius: 7px; cursor: pointer; color: var(--text-2); font-weight: 550; }
.seg button:hover { color: var(--text); }
.seg button.active { background: var(--plum-600); color: #fff; }
.custom { display: flex; gap: 6px; align-items: center; }
.custom .input { width: 140px; }
</style>
