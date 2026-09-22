<script setup lang="ts">
import { watch } from 'vue'
import { catalogBranch, setCatalogBranch, useLookups } from '../store'

// The whole Products section is viewed per branch; `requireBranch` pages (Склад) can't show "all".
const props = withDefaults(defineProps<{ requireBranch?: boolean }>(), { requireBranch: false })
const { lookups } = useLookups()

watch(lookups, l => {
  if (props.requireBranch && catalogBranch.value === '' && l?.branches[0]) setCatalogBranch(l.branches[0].id)
}, { immediate: true })

function change(ev: Event) {
  const v = (ev.target as HTMLSelectElement).value
  setCatalogBranch(v ? Number(v) : '')
}
</script>

<template>
  <label class="branch">
    <span>Филиал</span>
    <select class="select" :value="catalogBranch" @change="change">
      <option v-if="!requireBranch" value="">Все филиалы</option>
      <option v-for="b in lookups?.branches" :key="b.id" :value="b.id">{{ b.name }}</option>
    </select>
  </label>
</template>

<style scoped>
.branch { display: inline-flex; align-items: center; gap: 8px; }
.branch span { font-size: 12px; font-weight: 600; color: var(--text-2); }
</style>
