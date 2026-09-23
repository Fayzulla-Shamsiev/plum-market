<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { chatApi, type ChatSettings } from '../../api'
import Modal from '../../components/Modal.vue'

const emit = defineEmits<{ close: [] }>()
const s = ref<ChatSettings | null>(null)
// MVP: one channel — the storefront chat.
const channel = 'Website'
const saving = ref(false)
const error = ref('')

onMounted(async () => { s.value = await chatApi.settings() })

async function save() {
  if (!s.value) return
  saving.value = true
  error.value = ''
  try {
    s.value = await chatApi.saveSettings(s.value)
    emit('close')
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Modal title="Настройки чата" side width="420px" persistent @close="emit('close')">
    <div v-if="!s" class="skeleton" style="height: 300px" />
    <div v-else class="stack">
      <label class="switch opt">
        <input v-model="s.autoReplyEnabled" type="checkbox" /><span class="track" />
        <span><b>Автоматический ответ</b><small>Отправляется на первое сообщение клиента (не чаще раза в 6 часов на диалог)</small></span>
      </label>

      <div class="field" :class="{ off: !s.autoReplyEnabled }">
        <span>Текст автоответа</span>
        <textarea v-model="s.autoReplies[channel]" class="textarea" rows="4" :disabled="!s.autoReplyEnabled"
                  placeholder="Пусто — автоответ не отправляется" />
      </div>
      <div v-if="error" class="error-banner">{{ error }}</div>
    </div>
    <template #footer>
      <button class="btn" @click="emit('close')">Отмена</button>
      <button class="btn btn-primary" :disabled="!s || saving" @click="save">Сохранить</button>
    </template>
  </Modal>
</template>

<style scoped>
.stack { display: flex; flex-direction: column; gap: 18px; }
.opt { align-items: flex-start; }
.opt .track { margin-top: 2px; }
.opt small { display: block; color: var(--text-2); font-size: 12px; margin-top: 2px; }
.tabs { display: flex; flex-wrap: wrap; gap: 6px; margin-bottom: 6px; }
.chip.active :deep(.pf) { color: #fff; }
.off { opacity: .55; }
</style>
