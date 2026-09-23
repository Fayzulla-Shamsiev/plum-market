<script setup lang="ts">
import { computed } from 'vue'
import Icon from './Icon.vue'
import { delta } from '../format'

export interface KpiLine { label: string; value: string; color?: string }

const props = defineProps<{
  title: string
  icon: string
  value: string
  /** Omit both to hide the "vs previous period" delta (e.g. all-time figures). */
  current?: number
  previous?: number
  lines: KpiLine[]
}>()

const change = computed(() => (props.current === undefined || props.previous === undefined ? null : delta(props.current, props.previous)))
</script>

<template>
  <div class="card card-pad kpi">
    <div class="top">
      <span class="ic"><Icon :name="icon" /></span>
      <span class="title">{{ title }}</span>
      <span v-if="change !== null" class="delta" :class="change >= 0 ? 'up' : 'down'"
            :title="'К предыдущему периоду'">
        <Icon :name="change >= 0 ? 'arrowUp' : 'arrowDown'" />{{ Math.abs(change).toFixed(1) }}%
      </span>
    </div>
    <div class="value num">{{ value }}</div>
    <dl>
      <template v-for="l in lines" :key="l.label">
        <dt><i v-if="l.color" :style="{ background: l.color }" />{{ l.label }}</dt>
        <dd class="num">{{ l.value }}</dd>
      </template>
    </dl>
  </div>
</template>

<style scoped>
.kpi { display: flex; flex-direction: column; gap: 6px; }
.top { display: flex; align-items: center; gap: 8px; }
.ic { width: 30px; height: 30px; border-radius: 8px; background: var(--plum-50); color: var(--plum-600); display: grid; place-items: center; }
.ic svg { width: 17px; height: 17px; }
.title { font-weight: 600; color: var(--text-2); margin-right: auto; }
.delta { display: inline-flex; align-items: center; gap: 2px; font-size: 12px; font-weight: 650; padding: 2px 7px; border-radius: 10px; }
.delta svg { width: 12px; height: 12px; }
.delta.up { color: var(--good); background: var(--good-bg); }
.delta.down { color: var(--bad); background: var(--bad-bg); }
.value { font-size: 26px; font-weight: 700; letter-spacing: -0.02em; margin: 2px 0 4px; }
dl { display: grid; grid-template-columns: 1fr auto; gap: 5px 12px; margin: 0; padding-top: 10px; border-top: 1px dashed var(--border); }
dt { color: var(--text-2); display: flex; align-items: center; gap: 7px; }
dt i { width: 8px; height: 8px; border-radius: 2px; }
dd { margin: 0; text-align: right; font-weight: 600; white-space: nowrap; }
</style>
