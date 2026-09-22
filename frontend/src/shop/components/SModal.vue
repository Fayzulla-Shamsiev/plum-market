<script setup lang="ts">
import { onBeforeUnmount, onMounted } from 'vue'
import { t } from '../i18n'
import SIcon from './SIcon.vue'

// Storefront dialog: centered on desktop, a bottom sheet on phones.
defineProps<{ title: string; width?: string }>()
const emit = defineEmits<{ close: [] }>()
const onKey = (e: KeyboardEvent) => e.key === 'Escape' && emit('close')
onMounted(() => { document.addEventListener('keydown', onKey); document.body.style.overflow = 'hidden' })
onBeforeUnmount(() => { document.removeEventListener('keydown', onKey); document.body.style.overflow = '' })
</script>

<template>
  <Teleport to="body">
    <div class="shop s-modal-scrim" @mousedown.self="emit('close')">
      <section class="s-modal" role="dialog" aria-modal="true" :aria-label="title" :style="{ width: width ?? '440px' }">
        <header>
          <h2>{{ title }}</h2>
          <button type="button" class="x" :aria-label="t('close')" @click="emit('close')"><SIcon name="close" /></button>
        </header>
        <div class="body"><slot /></div>
        <footer v-if="$slots.footer"><slot name="footer" /></footer>
      </section>
    </div>
  </Teleport>
</template>

<style scoped>
.s-modal-scrim { position: fixed; inset: 0; z-index: 140; background: rgb(21 32 51 / 45%); display: grid; place-items: center; padding: 20px; min-height: 0; animation: fade .15s; }
@keyframes fade { from { opacity: 0; } }
.s-modal { max-width: 100%; max-height: calc(100vh - 40px); background: var(--card); border-radius: 22px; box-shadow: var(--lift-lg); display: flex; flex-direction: column; animation: pop .18s ease-out; }
@keyframes pop { from { transform: scale(.97); opacity: 0; } }
header { display: flex; align-items: center; gap: 12px; padding: 18px 18px 6px 22px; }
h2 { font-size: 20px; margin: 0; flex: 1; letter-spacing: -0.015em; }
.x { width: 38px; height: 38px; border: 0; border-radius: 11px; background: var(--page); cursor: pointer; display: grid; place-items: center; }
.body { padding: 10px 22px 18px; overflow-y: auto; }
footer { display: flex; gap: 10px; padding: 0 22px 22px; }
footer :deep(.s-btn) { flex: 1; }
@media (max-width: 640px) {
  .s-modal-scrim { place-items: end stretch; padding: 0; }
  .s-modal { width: 100% !important; border-radius: 22px 22px 0 0; max-height: 92vh; padding-bottom: env(safe-area-inset-bottom); animation: up .2s ease-out; }
  @keyframes up { from { transform: translateY(30px); opacity: .5; } }
}
</style>
