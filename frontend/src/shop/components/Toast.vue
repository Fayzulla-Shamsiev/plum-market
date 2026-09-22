<script setup lang="ts">
import { toast } from '../state/toast'
import SIcon from './SIcon.vue'

const dismiss = () => { toast.value = null }
</script>

<template>
  <Transition name="toast">
    <div v-if="toast" :key="toast.id" class="toast" role="status">
      <SIcon name="check" :size="18" class="ok" />
      <span>{{ toast.text }}</span>
      <RouterLink v-if="toast.to" :to="toast.to" class="act" @click="dismiss">{{ toast.action }}</RouterLink>
    </div>
  </Transition>
</template>

<style scoped>
.toast {
  position: fixed; left: 50%; bottom: 28px; transform: translateX(-50%); z-index: 120; display: flex; align-items: center; gap: 10px;
  background: var(--ink); color: #fff; padding: 12px 16px; border-radius: 14px; box-shadow: var(--lift-lg); font-size: 14.5px;
  max-width: calc(100vw - 28px); white-space: nowrap;
}
.ok { color: #57e39a; flex: none; }
.act { color: #8cc0ff !important; font-weight: 650; margin-left: 6px; }
.toast-enter-active, .toast-leave-active { transition: opacity .2s, transform .2s; }
.toast-enter-from, .toast-leave-to { opacity: 0; transform: translate(-50%, 10px); }
@media (max-width: 640px) { .toast { bottom: 84px; } }
</style>
