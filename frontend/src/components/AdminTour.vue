<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import { endTour, tourPage, tourSidebar } from '../tour'
import { tours, type TourStep } from '../tours'
import Icon from './Icon.vue'

// The guided tour of the page the administrator is on (tours.ts): the page dims, one part of it is spotlit, and a box
// explains what it does and what it keeps. Steps whose element isn't on the page are skipped or shown centred.

const steps = computed<TourStep[]>(() => tours[tourPage.value ?? ''] ?? [])
const index = ref(0)
const step = computed(() => steps.value[index.value])
const last = computed(() => index.value === steps.value.length - 1)
const target = ref<Element | null>(null)
const rect = ref<{ top: number; left: number; width: number; height: number } | null>(null)
const boxEl = ref<HTMLElement>()
const box = ref({ top: 0, left: 0, width: 360 })
const busy = ref(false)
const mobile = () => window.innerWidth <= 960

/** Waits for a page's element to appear: pages load their data after they open. */
async function find(selectors: string[] | undefined): Promise<Element | null> {
  if (!selectors?.length) return null
  for (let i = 0; i < 40; i++) {
    for (const s of selectors) {
      const el = document.querySelector(s)
      if (el && el.getBoundingClientRect().width > 0) return el
    }
    await new Promise(r => setTimeout(r, 75))
  }
  return null
}

async function show(i: number, direction = 1) {
  busy.value = true
  index.value = i
  const s = steps.value[i]
  target.value = null
  rect.value = null
  await nextTick()
  tourSidebar.value = !!s.menu && mobile()
  if (tourSidebar.value) await new Promise(r => setTimeout(r, 230)) // the drawer slides in
  const el = await find(s.target)
  if (index.value !== i) return
  // Nothing to point at (e.g. «Первые шаги» once orders come in): an optional step is simply passed.
  if (!el && s.optional) {
    const n = i + direction
    if (n >= 0 && n < steps.value.length) return show(n, direction)
  }
  if (el && !s.menu) el.scrollIntoView({ block: 'center', behavior: 'instant' as ScrollBehavior })
  target.value = el
  busy.value = false
  place()
}

const next = () => (last.value ? endTour() : !busy.value && show(index.value + 1, 1))
const back = () => index.value > 0 && !busy.value && show(index.value - 1, -1)

/** Keeps the spotlight on its element while the page scrolls, loads or the sidebar slides. */
let frame = 0
function place() {
  cancelAnimationFrame(frame)
  const vw = window.innerWidth
  const vh = window.innerHeight
  const width = Math.min(360, vw - 24)
  const height = boxEl.value?.offsetHeight ?? 200
  const el = target.value
  if (el) {
    const r = el.getBoundingClientRect()
    const pad = 6
    // A tall element (a whole table) is only lit where it is on screen.
    const top = Math.max(r.top - pad, 8)
    const bottom = Math.min(r.bottom + pad, vh - 8)
    rect.value = { top, left: r.left - pad, width: r.width + pad * 2, height: Math.max(bottom - top, 24) }
    const R = rect.value
    const clampX = (x: number) => Math.min(Math.max(x, 12), vw - width - 12)
    const clampY = (y: number) => Math.min(Math.max(y, 12), vh - height - 12)
    if (R.left + R.width + 16 + width <= vw - 12 && R.width < vw * 0.45)
      box.value = { left: R.left + R.width + 16, top: clampY(R.top + R.height / 2 - height / 2), width }
    else if (R.top + R.height + 14 + height <= vh - 12)
      box.value = { left: clampX(R.left + R.width / 2 - width / 2), top: R.top + R.height + 14, width }
    else if (R.top - 14 - height >= 12)
      box.value = { left: clampX(R.left + R.width / 2 - width / 2), top: R.top - 14 - height, width }
    else
      box.value = { left: clampX(vw / 2 - width / 2), top: vh - height - 16, width }
  } else {
    rect.value = null
    box.value = { left: vw / 2 - width / 2, top: Math.max(vh / 2 - height / 2, 12), width }
  }
  frame = requestAnimationFrame(place)
}

function onKey(e: KeyboardEvent) {
  if (e.key === 'Escape') endTour()
  else if (e.key === 'ArrowRight' || e.key === 'Enter') { e.preventDefault(); next() }
  else if (e.key === 'ArrowLeft') back()
}

onMounted(() => {
  window.addEventListener('keydown', onKey)
  // The page has only just opened: give it a moment to render before looking for its parts.
  setTimeout(() => show(0), 250)
})
onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKey)
  cancelAnimationFrame(frame)
  tourSidebar.value = false
})
</script>

<template>
  <div v-if="step" class="tour" role="dialog" aria-modal="true" :aria-label="`Подсказка: ${step.title}`">
    <!-- Catches clicks: during the tour the panel is to be looked at, not used. -->
    <div class="catch" :class="{ plain: !rect }" />
    <div v-if="rect" class="spot" :style="{ top: `${rect.top}px`, left: `${rect.left}px`, width: `${rect.width}px`, height: `${rect.height}px` }" />

    <section ref="boxEl" class="box" :class="{ center: !rect }" :style="{ top: `${box.top}px`, left: `${box.left}px`, width: `${box.width}px` }">
      <div class="box-top">
        <span class="count">{{ index + 1 }} / {{ steps.length }}</span>
        <button class="skip" @click="endTour">{{ last ? 'Закрыть' : 'Пропустить' }}</button>
      </div>
      <h2>{{ step.title }}</h2>
      <p>{{ step.text }}</p>
      <div class="dots" aria-hidden="true">
        <i v-for="(_, i) in steps" :key="i" :class="{ on: i === index, done: i < index }" />
      </div>
      <div class="box-foot">
        <button v-if="index > 0" class="btn btn-ghost" :disabled="busy" @click="back"><Icon name="chevronLeft" />Назад</button>
        <button class="btn btn-primary" :disabled="busy && !last" @click="next">
          {{ last ? 'Понятно' : 'Далее' }}<Icon v-if="!last" name="chevronRight" />
        </button>
      </div>
    </section>
  </div>
</template>

<style scoped>
.tour { position: fixed; inset: 0; z-index: 1000; }
.catch { position: absolute; inset: 0; }
/* Without a spotlight the dim comes from here; with one, from the spotlight's shadow. */
.catch.plain { background: rgb(20 8 30 / 62%); }
.spot {
  position: absolute; border-radius: 12px; pointer-events: none;
  box-shadow: 0 0 0 9999px rgb(20 8 30 / 62%), 0 0 0 3px rgb(255 255 255 / 85%), 0 0 24px 6px rgb(197 139 224 / 55%);
  transition: top .25s ease, left .25s ease, width .25s ease, height .25s ease;
}
.box {
  position: absolute; background: var(--surface); border-radius: 14px; padding: 16px 18px 14px;
  box-shadow: var(--shadow-lg); display: flex; flex-direction: column; gap: 8px;
  transition: top .25s ease, left .25s ease;
}
.box.center { transition: none; }
.box-top { display: flex; align-items: center; }
.count { font-size: 12px; font-weight: 650; color: var(--plum-600); background: var(--plum-50); padding: 2px 8px; border-radius: 999px; }
.skip { margin-left: auto; border: 0; background: none; color: var(--text-3); font: inherit; font-size: 13px; cursor: pointer; padding: 2px 4px; }
.skip:hover { color: var(--text); text-decoration: underline; }
h2 { font-size: 16.5px; }
p { margin: 0; color: var(--text-2); font-size: 14px; line-height: 1.55; }
.dots { display: flex; flex-wrap: wrap; gap: 4px; margin-top: 2px; }
.dots i { width: 6px; height: 6px; border-radius: 50%; background: var(--border-strong); }
.dots i.done { background: var(--plum-500); opacity: .45; }
.dots i.on { background: var(--plum-600); width: 16px; border-radius: 3px; }
.box-foot { display: flex; justify-content: flex-end; gap: 8px; margin-top: 4px; }
</style>
