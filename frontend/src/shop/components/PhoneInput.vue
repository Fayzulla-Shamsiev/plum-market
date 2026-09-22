<script setup lang="ts">
import { computed } from 'vue'
import { formatPhone } from '../i18n'

// Uzbek mobile number: fixed "+998" and 9 digits shown as "90 123 45 67". v-model holds just the 9 digits.
const model = defineModel<string>({ required: true })
defineProps<{ invalid?: boolean; autofocus?: boolean }>()
const shown = computed(() => formatPhone(model.value))
function onInput(e: Event) {
  const el = e.target as HTMLInputElement
  let digits = el.value.replace(/\D/g, '')
  // Pasted with the country code.
  if (digits.length > 9 && digits.startsWith('998')) digits = digits.slice(3)
  model.value = digits.slice(0, 9)
  el.value = formatPhone(model.value)
}
</script>

<template>
  <div class="phone" :class="{ invalid }">
    <span class="cc">+998</span>
    <input :value="shown" type="tel" inputmode="numeric" autocomplete="tel-national" placeholder="90 123 45 67"
      :autofocus="autofocus" @input="onInput" />
  </div>
</template>

<style scoped>
.phone { display: flex; align-items: center; height: 50px; border: 1.5px solid var(--line); border-radius: 14px; background: var(--card); transition: border-color .15s, box-shadow .15s; }
.phone:focus-within { border-color: var(--blue); box-shadow: 0 0 0 4px var(--blue-50); }
.phone.invalid { border-color: var(--red); }
.cc { padding: 0 4px 0 14px; font-weight: 600; color: var(--ink-2); }
input { flex: 1; min-width: 0; border: 0; outline: none; background: none; font: inherit; font-size: 16px; padding: 0 12px 0 6px; height: 100%; letter-spacing: .02em; }
.shop input:focus-visible { outline: none; }
</style>
