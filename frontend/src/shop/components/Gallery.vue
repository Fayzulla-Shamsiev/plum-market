<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { t } from '../i18n'
import { emojiFor, tintFor } from '../visuals'
import SIcon from './SIcon.vue'

// Product photos and videos: thumbnails beside the main view on desktop, swipe with dots on phones,
// and a full-screen viewer. With no media uploaded it shows the generated placeholder.
const props = defineProps<{ id: number; name: string; media: { url: string; type: string }[] }>()
const i = ref(0)
const zoom = ref(false)
const track = ref<HTMLElement>()
const items = computed(() => props.media.filter(m => m.type === 'image' || m.type === 'video'))
watch(() => props.id, () => { i.value = 0 })

// While a thumbnail/arrow scrolls the track, ignore the scroll events so the highlight doesn't jump back.
let steeringUntil = 0
function go(n: number) {
  const len = items.value.length
  if (!len) return
  i.value = (n + len) % len
  steeringUntil = Date.now() + 700
  track.value?.scrollTo({ left: track.value.clientWidth * i.value, behavior: 'smooth' })
}
// Phone swipe: follow the scroll-snap position.
const onScroll = () => {
  const el = track.value
  if (el && Date.now() > steeringUntil) i.value = Math.round(el.scrollLeft / el.clientWidth)
}
function onKey(e: KeyboardEvent) {
  if (!zoom.value) return
  if (e.key === 'Escape') zoom.value = false
  else if (e.key === 'ArrowRight') go(i.value + 1)
  else if (e.key === 'ArrowLeft') go(i.value - 1)
}
watch(zoom, z => {
  document.body.style.overflow = z ? 'hidden' : ''
  if (z) document.addEventListener('keydown', onKey)
  else document.removeEventListener('keydown', onKey)
})
onBeforeUnmount(() => { document.removeEventListener('keydown', onKey); document.body.style.overflow = '' })
</script>

<template>
  <div class="gallery" :class="{ single: items.length < 2 }">
    <div v-if="items.length > 1" class="thumbs">
      <button v-for="(m, n) in items" :key="m.url" type="button" :class="{ on: n === i }" :aria-label="`${t('photo')} ${n + 1}`" @click="go(n)">
        <img v-if="m.type === 'image'" :src="m.url" alt="" />
        <span v-else class="vid">▶</span>
      </button>
    </div>

    <div class="stage">
      <div v-if="items.length" ref="track" class="track" @scroll.passive="onScroll">
        <div v-for="(m, n) in items" :key="m.url" class="slide">
          <img v-if="m.type === 'image'" :src="m.url" :alt="`${name} — ${n + 1}`" @click="zoom = true" />
          <video v-else :src="m.url" controls playsinline preload="metadata" />
        </div>
      </div>
      <div v-else class="ph" :style="{ background: tintFor(id) }" aria-hidden="true"><span>{{ emojiFor(name) }}</span></div>

      <template v-if="items.length > 1">
        <button class="nav prev" :aria-label="t('back')" @click="go(i - 1)"><SIcon name="chevronLeft" /></button>
        <button class="nav next" aria-label="→" @click="go(i + 1)"><SIcon name="chevronRight" /></button>
        <div class="dots"><span v-for="(m, n) in items" :key="m.url" :class="{ on: n === i }" /></div>
      </template>
      <slot />
    </div>

    <Teleport to="body">
      <div v-if="zoom" class="viewer" @click.self="zoom = false">
        <button class="v-close" :aria-label="t('close')" @click="zoom = false"><SIcon name="close" /></button>
        <img :src="items[i]?.url" :alt="name" />
        <template v-if="items.length > 1">
          <button class="v-nav prev" :aria-label="t('back')" @click="go(i - 1)"><SIcon name="chevronLeft" :size="26" /></button>
          <button class="v-nav next" aria-label="→" @click="go(i + 1)"><SIcon name="chevronRight" :size="26" /></button>
          <div class="v-count">{{ i + 1 }} / {{ items.length }}</div>
        </template>
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.gallery { display: grid; grid-template-columns: 76px 1fr; gap: 12px; align-items: start; }
.gallery.single { grid-template-columns: 1fr; }
.thumbs { display: flex; flex-direction: column; gap: 8px; max-height: 560px; overflow-y: auto; scrollbar-width: none; }
.thumbs button { width: 76px; height: 76px; border-radius: 14px; overflow: hidden; border: 2px solid transparent; padding: 0; background: var(--page); cursor: pointer; flex: none; }
.thumbs button.on { border-color: var(--blue); }
.thumbs img { width: 100%; height: 100%; object-fit: cover; }
.vid { display: grid; place-items: center; height: 100%; color: var(--ink-2); }
.stage { position: relative; border-radius: 22px; overflow: hidden; background: var(--page); aspect-ratio: 1; }
.track { display: flex; height: 100%; overflow-x: auto; scroll-snap-type: x mandatory; scrollbar-width: none; }
.track::-webkit-scrollbar { display: none; }
.slide { flex: 0 0 100%; scroll-snap-align: start; display: grid; place-items: center; }
.slide img { width: 100%; height: 100%; object-fit: cover; cursor: zoom-in; }
.slide video { width: 100%; height: 100%; object-fit: contain; background: #000; }
.ph { height: 100%; display: grid; place-items: center; container-type: inline-size; }
.ph span { font-size: 42cqw; line-height: 1; filter: drop-shadow(0 12px 18px rgb(21 32 51 / 16%)); }
.nav {
  position: absolute; top: 50%; transform: translateY(-50%); width: 42px; height: 42px; border-radius: 50%; border: 0;
  background: rgb(255 255 255 / 90%); box-shadow: var(--lift); cursor: pointer; display: grid; place-items: center; opacity: 0; transition: opacity .2s;
}
.stage:hover .nav { opacity: 1; }
.prev { left: 12px; }
.next { right: 12px; }
.dots { position: absolute; left: 0; right: 0; bottom: 12px; display: flex; justify-content: center; gap: 5px; pointer-events: none; }
.dots span { width: 7px; height: 7px; border-radius: 4px; background: rgb(21 32 51 / 25%); transition: width .2s, background .2s; }
.dots span.on { width: 20px; background: var(--blue); }

.viewer { position: fixed; inset: 0; z-index: 130; background: rgb(10 14 22 / 94%); display: grid; place-items: center; padding: 40px; }
.viewer img { max-width: 100%; max-height: 100%; object-fit: contain; border-radius: 8px; }
.v-close, .v-nav { position: absolute; border: 0; background: rgb(255 255 255 / 12%); color: #fff; border-radius: 50%; cursor: pointer; display: grid; place-items: center; }
.v-close { right: 18px; top: 18px; width: 44px; height: 44px; }
.v-nav { top: 50%; transform: translateY(-50%); width: 54px; height: 54px; }
.v-nav.prev { left: 18px; }
.v-nav.next { right: 18px; }
.v-close:hover, .v-nav:hover { background: rgb(255 255 255 / 22%); }
.v-count { position: absolute; bottom: 18px; color: #fff; opacity: .7; font-size: 14px; }

@media (max-width: 900px) {
  .gallery { grid-template-columns: 1fr; }
  .thumbs { display: none; }
  .nav { display: none; }
  .stage { border-radius: 18px; }
}
</style>
