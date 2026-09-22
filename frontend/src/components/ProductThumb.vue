<script setup lang="ts">
import { computed } from 'vue'

/** Cover image, or a tinted initial when the product has no photo yet. */
const props = withDefaults(defineProps<{ url?: string | null; name: string; size?: number }>(), { url: null, size: 40 })
const hue = computed(() => [...props.name].reduce((h, c) => (h * 31 + c.charCodeAt(0)) % 360, 7))
</script>

<template>
  <img v-if="url" :src="url" alt="" class="thumb" :style="{ width: size + 'px', height: size + 'px' }" />
  <span v-else class="thumb ph" :style="{ width: size + 'px', height: size + 'px', background: `hsl(${hue} 55% 92%)`, color: `hsl(${hue} 45% 35%)` }">
    {{ name.trim()[0]?.toUpperCase() }}
  </span>
</template>

<style scoped>
.thumb { border-radius: 9px; object-fit: cover; flex: none; display: inline-block; }
.ph { display: inline-grid; place-items: center; font-weight: 700; }
</style>
