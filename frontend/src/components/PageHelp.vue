<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { startTour, tourActive } from '../tour'
import { tours } from '../tours'
import Icon from './Icon.vue'

// «?» next to a page's title: replays the short tour of this page only (tours.ts). The full walkthrough of the whole
// panel is «?» at the bottom of the menu.
const route = useRoute()
const page = computed(() => route.meta.tour as string | undefined)
</script>

<template>
  <button v-if="page && tours[page]?.length" type="button" class="page-help" title="Подсказка по этой странице"
    aria-label="Подсказка по этой странице" :disabled="tourActive" @click="startTour(page)">
    <Icon name="help" />
  </button>
</template>

<style scoped>
.page-help {
  display: inline-grid; place-items: center; width: 26px; height: 26px; margin-left: 8px; vertical-align: 3px;
  border: 1px solid var(--border); border-radius: 50%; background: var(--surface); color: var(--text-3); cursor: pointer;
  transition: color .15s, border-color .15s, background .15s;
}
.page-help:hover:not(:disabled) { color: var(--plum-600); border-color: var(--plum-100); background: var(--plum-50); }
.page-help:disabled { opacity: .5; cursor: default; }
.page-help svg { width: 15px; height: 15px; }
</style>
