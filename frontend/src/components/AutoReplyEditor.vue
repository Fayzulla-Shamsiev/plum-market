<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { api, type AutoReply, type OrderStatus } from '../api'
import { languageLabel, statusLabel } from '../format'
import { useLookups } from '../store'

/**
 * «Автоответчик»: what the customer is told at each step of their order, per language. The same text is sent
 * into their chat with the store and, when the shop has a Telegram bot they have opened, into Telegram.
 * Used both in Заказы (a modal) and in Платформы → Telegram-бот (a tab), so it lives on its own.
 */
const emit = defineEmits<{ saved: [] }>()
const { lookups } = useLookups()

const items = ref<AutoReply[]>([])
const saving = ref(false)
const error = ref('')
const lang = ref('ru')
const languages = computed(() => lookups.value?.store.languages ?? ['ru', 'uz', 'en'])
const statuses = Object.keys(statusLabel) as OrderStatus[]
const placeholders = ['{name}', '{order_id}', '{total}', '{branch}', '{bonus}']

onMounted(async () => {
  const loaded = await api.autoReplies()
  // Make sure every status × language pair has an editable row.
  for (const s of statuses)
    for (const l of languages.value)
      if (!loaded.some(t => t.status === s && t.language === l)) loaded.push({ status: s, language: l, enabled: false, text: '' })
  items.value = loaded
})

const current = computed(() => statuses.map(s => items.value.find(t => t.status === s && t.language === lang.value)!).filter(Boolean))
const preview = (text: string) => text
  .replace(/\{name\}/g, 'Азиз').replace(/\{order_id\}/g, '1024').replace(/\{total\}/g, '145 000 сум')
  .replace(/\{branch\}/g, 'Чиланзар').replace(/\{bonus\}/g, '1 450')

function insert(t: AutoReply, ph: string) {
  t.text = (t.text ? t.text + ' ' : '') + ph
}

async function save() {
  saving.value = true
  error.value = ''
  try {
    await api.saveAutoReplies(items.value)
    emit('saved')
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    saving.value = false
  }
}

defineExpose({ save, saving })
</script>

<template>
  <div class="autoreply">
    <div class="langs" role="tablist">
      <button v-for="l in languages" :key="l" class="chip" :class="{ active: lang === l }" role="tab" @click="lang = l">
        {{ languageLabel[l] ?? l }}
      </button>
    </div>
    <div v-if="error" class="error-banner">{{ error }}</div>
    <div v-if="!items.length" class="skeleton" style="height: 300px" />
    <div class="list">
      <div v-for="t in current" :key="t.status + t.language" class="tpl" :class="{ off: !t.enabled }">
        <div class="tpl-head">
          <span class="badge" :class="t.status">{{ statusLabel[t.status] }}</span>
          <label class="switch"><input v-model="t.enabled" type="checkbox" /><span class="track" /></label>
        </div>
        <textarea v-model="t.text" class="textarea" rows="2" :disabled="!t.enabled" :aria-label="`Текст для статуса ${statusLabel[t.status]}`" />
        <div class="tpl-foot">
          <span class="phs">
            <button v-for="ph in placeholders" :key="ph" type="button" class="ph" :disabled="!t.enabled" @click="insert(t, ph)">{{ ph }}</button>
          </span>
        </div>
        <div v-if="t.enabled && t.text" class="preview">{{ preview(t.text) }}</div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.langs { display: flex; gap: 8px; margin-bottom: 14px; }
.list { display: flex; flex-direction: column; gap: 12px; }
.tpl { border: 1px solid var(--border); border-radius: 10px; padding: 10px 12px; display: flex; flex-direction: column; gap: 8px; }
.tpl.off { background: var(--surface-2); }
.tpl-head { display: flex; justify-content: space-between; align-items: center; }
.phs { display: flex; gap: 4px; flex-wrap: wrap; }
.ph { border: 1px dashed var(--border-strong); background: none; border-radius: 6px; font: 12px ui-monospace, monospace; padding: 2px 6px; cursor: pointer; color: var(--text-2); }
.ph:hover:not(:disabled) { border-color: var(--plum-500); color: var(--plum-600); }
.preview { background: #e7f3fd; border-radius: 12px 12px 12px 3px; padding: 7px 11px; font-size: 13px; align-self: flex-start; max-width: 90%; }
</style>
