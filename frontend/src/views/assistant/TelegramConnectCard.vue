<script setup lang="ts">
import { ref } from 'vue'
import { api } from '../../api'

// The bot token controls the whole bot, so it is typed here and goes straight to Платформы → Telegram-бот —
// never into the conversation, and never to the AI.
defineProps<{ connected: string | null }>()
const emit = defineEmits<{ connected: [username: string] }>()

const token = ref('')
const busy = ref(false)
const error = ref('')

async function connect() {
  if (busy.value) return
  if (!token.value.trim()) {
    error.value = 'Вставьте токен, который прислал @BotFather.'
    return
  }
  busy.value = true
  error.value = ''
  try {
    const p = await api.connectBot(token.value.trim())
    token.value = ''
    if (p.telegram) emit('connected', p.telegram.username)
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="tg">
    <div class="tg-head">
      <svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="12" fill="#27a6e5" /><path d="M5.4 11.7 17 7.2c.5-.2 1 .1.8.9l-2 9.3c-.1.6-.5.8-1 .5l-2.9-2.1-1.4 1.3c-.2.2-.3.3-.6.3l.2-2.9 5.4-4.9c.2-.2 0-.3-.3-.1l-6.7 4.2-2.8-.9c-.6-.2-.6-.6.1-.9Z" fill="#fff" /></svg>
      <b>Подключение Telegram-бота</b>
    </div>
    <p v-if="connected" class="ok">Бот <b>@{{ connected }}</b> подключён — магазин открывается кнопкой «Open Shop» в боте.</p>
    <template v-else>
      <ol>
        <li>Откройте <a href="https://t.me/BotFather" target="_blank" rel="noopener">@BotFather</a> и отправьте <code>/newbot</code>.</li>
        <li>Придумайте название и имя бота — оно должно заканчиваться на «bot».</li>
        <li>Скопируйте токен из ответа и вставьте сюда.</li>
      </ol>
      <form class="row" @submit.prevent="connect">
        <input v-model="token" class="in" autocomplete="off" spellcheck="false" placeholder="123456789:AA…" aria-label="Токен бота" />
        <button class="go" :disabled="busy">{{ busy ? 'Проверяем…' : 'Подключить' }}</button>
      </form>
      <p v-if="error" class="err">{{ error }}</p>
      <small>Токен уходит прямо в настройки магазина и не попадает в чат с помощником.</small>
    </template>
  </div>
</template>

<style scoped>
.tg { display: flex; flex-direction: column; gap: 10px; }
.tg-head { display: flex; align-items: center; gap: 8px; font-size: 14px; }
.tg-head svg { width: 22px; height: 22px; flex: none; }
ol { margin: 0; padding-left: 18px; display: grid; gap: 4px; font-size: 13.5px; color: var(--ink-2); }
ol a { color: var(--blue); font-weight: 600; }
code { font-size: 12.5px; padding: 1px 5px; border-radius: 5px; background: #eef2f8; }
.row { display: flex; gap: 8px; }
.in {
  flex: 1; min-width: 0; height: 40px; border: 1.5px solid var(--line); border-radius: 10px; padding: 0 12px;
  font: inherit; font-size: 14px; font-family: ui-monospace, SFMono-Regular, Menlo, monospace; outline: none; background: #fff; color: var(--ink);
}
.in:focus { border-color: var(--blue); box-shadow: 0 0 0 3px var(--blue-50); }
.go { height: 40px; padding: 0 16px; border: 0; border-radius: 10px; background: var(--blue); color: #fff; font: inherit; font-weight: 650; cursor: pointer; white-space: nowrap; }
.go:hover:not(:disabled) { background: var(--blue-600); }
.go:disabled { opacity: .6; cursor: default; }
.err { margin: 0; color: #b42318; font-size: 13px; }
.ok { margin: 0; font-size: 13.5px; color: var(--ink-2); }
small { color: var(--ink-3); font-size: 12px; }
</style>
