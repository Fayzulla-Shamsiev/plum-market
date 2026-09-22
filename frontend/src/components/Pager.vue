<script setup lang="ts">
import { computed } from 'vue'
import Icon from './Icon.vue'
import { count } from '../format'

const props = defineProps<{ page: number; pageSize: number; total: number }>()
const emit = defineEmits<{ 'update:page': [number] }>()
const pages = computed(() => Math.max(1, Math.ceil(props.total / props.pageSize)))
const from = computed(() => (props.total === 0 ? 0 : (props.page - 1) * props.pageSize + 1))
const to = computed(() => Math.min(props.total, props.page * props.pageSize))
</script>

<template>
  <div class="pager">
    <span class="muted num">{{ count(from) }}–{{ count(to) }} из {{ count(total) }}</span>
    <button class="btn btn-sm btn-icon" :disabled="page <= 1" aria-label="Назад" @click="emit('update:page', page - 1)">
      <Icon name="chevronLeft" />
    </button>
    <span class="num">{{ page }} / {{ pages }}</span>
    <button class="btn btn-sm btn-icon" :disabled="page >= pages" aria-label="Вперёд" @click="emit('update:page', page + 1)">
      <Icon name="chevronRight" />
    </button>
  </div>
</template>

<style scoped>
.pager { display: flex; align-items: center; justify-content: flex-end; gap: 10px; padding: 12px 16px; border-top: 1px solid var(--border); }
</style>
