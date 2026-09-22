<script setup lang="ts">
import SIcon from './SIcon.vue'

defineProps<{ to: string; label: string }>()
</script>

<template>
  <nav class="back"><RouterLink :to="to"><SIcon name="chevronLeft" :size="16" /> {{ label }}</RouterLink></nav>
</template>

<style scoped>
.back { padding: 18px 0 6px; font-size: 14px; }
.back a { display: inline-flex; align-items: center; gap: 2px; color: var(--ink-2); }
.back a:hover { color: var(--blue); }
</style>
