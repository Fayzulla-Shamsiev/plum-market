<script setup lang="ts">
import { nextTick, onMounted, ref } from 'vue'
import type { Card } from '../api'
import ProductCard from './ProductCard.vue'
import SIcon from './SIcon.vue'

// One horizontally scrolling row of product cards with prev/next arrows on desktop.
defineProps<{ items: Card[] }>()
const track = ref<HTMLElement>()
const canPrev = ref(false)
const canNext = ref(false)

function update() {
  const el = track.value
  if (!el) return
  canPrev.value = el.scrollLeft > 4
  canNext.value = el.scrollLeft + el.clientWidth < el.scrollWidth - 4
}
const page = (dir: 1 | -1) => track.value?.scrollBy({ left: dir * track.value.clientWidth * 0.85 })
onMounted(() => nextTick(update))
</script>

<template>
  <div class="rail">
    <div ref="track" class="track s-scroll" @scroll.passive="update">
      <ProductCard v-for="p in items" :key="p.id" :p="p" class="item" />
    </div>
    <button v-show="canPrev" class="arrow prev" aria-label="Назад" @click="page(-1)"><SIcon name="chevronLeft" /></button>
    <button v-show="canNext" class="arrow next" aria-label="Вперёд" @click="page(1)"><SIcon name="chevronRight" /></button>
  </div>
</template>

<style scoped>
.rail { position: relative; }
.track { gap: 16px; padding: 4px 2px 14px; margin: -4px -2px -14px; scroll-snap-type: x proximity; }
.item { flex: 0 0 204px; scroll-snap-align: start; }
.arrow {
  position: absolute; top: calc(50% - 40px); width: 42px; height: 42px; border-radius: 50%; border: 0; cursor: pointer;
  background: var(--card); box-shadow: var(--lift-lg); display: grid; place-items: center; color: var(--ink);
}
.arrow:hover { color: var(--blue); }
.prev { left: -14px; }
.next { right: -14px; }
@media (max-width: 640px) {
  .track { gap: 10px; }
  .item { flex-basis: 44%; }
  .arrow { display: none; }
}
</style>
