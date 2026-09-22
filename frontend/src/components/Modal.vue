<script setup lang="ts">
import { onBeforeUnmount, onMounted } from 'vue'
import Icon from './Icon.vue'

// Centered dialog; `side` turns it into a right-hand drawer.
// `persistent` dialogs (forms) ignore backdrop clicks so a stray click doesn't discard input.
const props = withDefaults(defineProps<{ title: string; width?: string; side?: boolean; persistent?: boolean }>(),
  { width: '520px', side: false, persistent: false })
const emit = defineEmits<{ close: [] }>()

const onKey = (e: KeyboardEvent) => e.key === 'Escape' && emit('close')
onMounted(() => document.addEventListener('keydown', onKey))
onBeforeUnmount(() => document.removeEventListener('keydown', onKey))
</script>

<template>
  <Teleport to="body">
    <div class="overlay" :class="{ side }" @mousedown.self="!props.persistent && emit('close')">
      <div class="dialog" role="dialog" aria-modal="true" :aria-label="title" :style="{ width }">
        <header>
          <h2>{{ title }}</h2>
          <slot name="head" />
          <button class="btn btn-ghost btn-icon" aria-label="Закрыть" @click="emit('close')"><Icon name="close" /></button>
        </header>
        <div class="body"><slot /></div>
        <footer v-if="$slots.footer"><slot name="footer" /></footer>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
.overlay { position: fixed; inset: 0; background: rgb(22 12 32 / 40%); z-index: 1000; display: flex; align-items: center; justify-content: center; padding: 16px; }
.dialog { background: var(--surface); border-radius: 14px; box-shadow: var(--shadow-lg); max-width: 100%; max-height: calc(100vh - 32px); display: flex; flex-direction: column; }
.overlay.side { justify-content: flex-end; padding: 0; align-items: stretch; }
.overlay.side .dialog { border-radius: 0; max-height: 100vh; height: 100vh; animation: slide .18s ease-out; }
@keyframes slide { from { transform: translateX(30px); opacity: .6; } }
header { display: flex; align-items: center; gap: 10px; padding: 14px 16px 14px 20px; border-bottom: 1px solid var(--border); }
header h2 { font-size: 16px; margin-right: auto; }
.body { padding: 18px 20px; overflow-y: auto; }
footer { display: flex; justify-content: flex-end; gap: 8px; padding: 12px 20px; border-top: 1px solid var(--border); }
</style>
