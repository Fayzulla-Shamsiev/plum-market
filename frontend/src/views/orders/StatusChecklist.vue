<script setup lang="ts">
import type { OrderStatus } from '../../api'
import { statusLabel } from '../../format'

const model = defineModel<OrderStatus[]>({ required: true })
const all = Object.keys(statusLabel) as OrderStatus[]
const toggleAll = () => (model.value = model.value.length === all.length ? [] : [...all])
</script>

<template>
  <div class="field">
    <span class="row" style="justify-content: space-between">
      Статусы заказов
      <button type="button" class="link" @click="toggleAll">{{ model.length === all.length ? 'Снять все' : 'Выбрать все' }}</button>
    </span>
    <div class="checks">
      <label v-for="s in all" :key="s" class="check">
        <input v-model="model" type="checkbox" :value="s" />
        <span class="badge" :class="s">{{ statusLabel[s] }}</span>
      </label>
    </div>
  </div>
</template>

<style scoped>
.checks { display: flex; flex-wrap: wrap; gap: 8px 14px; }
.check { display: inline-flex; align-items: center; gap: 6px; cursor: pointer; }
.check input { accent-color: var(--plum-600); width: 16px; height: 16px; }
.link { border: 0; background: none; color: var(--plum-600); font: inherit; font-size: 12px; cursor: pointer; padding: 0; }
</style>
