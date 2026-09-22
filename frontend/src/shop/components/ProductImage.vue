<script setup lang="ts">
import { computed, ref } from 'vue'
import { emojiFor, tintFor } from '../visuals'

const props = defineProps<{ id: number; name: string; src: string | null }>()
const failed = ref(false)
const emoji = computed(() => emojiFor(props.name))
</script>

<template>
  <div class="pimg" :style="{ background: tintFor(id) }">
    <img v-if="src && !failed" :src="src" :alt="name" loading="lazy" @error="failed = true" />
    <span v-else class="ph" aria-hidden="true">{{ emoji }}</span>
  </div>
</template>

<style scoped>
.pimg { container-type: inline-size; position: relative; width: 100%; aspect-ratio: 1; display: grid; place-items: center; overflow: hidden; }
img { width: 100%; height: 100%; object-fit: cover; }
.ph { font-size: 40cqw; line-height: 1; filter: drop-shadow(0 6px 10px rgb(21 32 51 / 14%)); transition: transform .25s; }
</style>
