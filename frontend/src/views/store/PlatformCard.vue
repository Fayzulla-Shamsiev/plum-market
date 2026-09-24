<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { api, type PlatformInfo } from '../../api'
import { admin } from '../../auth'
import Icon from '../../components/Icon.vue'

// «Платформа»: where this shop's customers open it. The choice is made at registration and can be changed here —
// connecting a Telegram bot attaches the shop to its "Open Shop" menu button through the Telegram Bot API.
const info = ref<PlatformInfo | null>(null)
const choice = ref<'Website' | 'Telegram'>('Website')
const botToken = ref('')
const busy = ref(false)
const error = ref('')
const done = ref('')

async function load() {
  info.value = await api.platform()
  choice.value = info.value.platform
}
onMounted(load)

async function save() {
  if (!info.value || busy.value) return
  busy.value = true
  error.value = ''
  done.value = ''
  try {
    info.value = await api.savePlatform({ platform: choice.value, botToken: botToken.value.trim() || undefined })
    choice.value = info.value.platform
    botToken.value = ''
    done.value = info.value.platform === 'Telegram'
      ? info.value.bot?.linkedAt ? 'Магазин привязан к боту.' : 'Бот подключён.'
      : 'Магазин открывается по ссылке.'
    // Keep the sidebar's "Открыть магазин" in step with the new platform.
    if (admin.value) admin.value.store = { ...admin.value.store, platform: info.value.platform, url: info.value.url, bot: info.value.bot }
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busy.value = false
  }
}

const copy = (text: string) => navigator.clipboard?.writeText(text).then(() => (done.value = 'Ссылка скопирована.'), () => {})
const changed = () => !!info.value && (choice.value !== info.value.platform || !!botToken.value.trim())
</script>

<template>
  <section class="card card-pad">
    <div class="card-head">
      <h2>Платформа</h2>
      <span class="card-sub">где покупатели открывают магазин</span>
    </div>

    <div v-if="!info" class="skeleton" style="height: 180px" />
    <template v-else>
      <div class="choices">
        <label :class="{ on: choice === 'Website' }">
          <input v-model="choice" type="radio" value="Website" />
          <b>Веб-сайт</b>
          <small>Магазин открывается по обычной ссылке в браузере.</small>
        </label>
        <label :class="{ on: choice === 'Telegram' }">
          <input v-model="choice" type="radio" value="Telegram" />
          <b>Telegram Mini App</b>
          <small>Магазин открывается внутри вашего бота по кнопке «Open Shop».</small>
        </label>
      </div>

      <div class="link">
        <span class="faint">Ссылка на магазин</span>
        <code>{{ info.url }}</code>
        <button type="button" class="btn btn-sm" @click="copy(info.url)"><Icon name="clipboard" />Скопировать</button>
        <a class="btn btn-sm" :href="info.url" target="_blank" rel="noopener">Открыть</a>
      </div>

      <div v-if="choice === 'Telegram'" class="tg">
        <div v-if="info.bot" class="bot-row">
          <div>
            <b>{{ info.bot.name }}</b>
            <a :href="info.bot.url" target="_blank" rel="noopener">@{{ info.bot.username }}</a>
          </div>
          <span v-if="info.bot.linkedAt" class="badge good">Кнопка «Open Shop» подключена</span>
          <span v-else class="badge warn">Кнопка не подключена</span>
        </div>
        <p v-if="info.bot?.warning" class="warning">{{ info.bot.warning }}</p>
        <template v-else-if="info.bot?.linkedAt">
          <p class="hint">
            Откройте бота и нажмите кнопку меню <b>«Open Shop»</b> слева от поля ввода — магазин откроется внутри Telegram.
          </p>
          <p v-if="info.bot.buttonIsFallback" class="warning">
            Кнопка открывает опубликованный прототип <code>{{ info.bot.buttonUrl }}</code>, потому что локальный адрес
            недоступен из Telegram. Подключите бота на опубликованном сайте — и кнопка будет открывать именно этот магазин.
          </p>
        </template>

        <label class="field">
          <span>{{ info.bot ? 'Другой бот — вставьте новый токен' : 'Токен бота из @BotFather' }}</span>
          <input v-model="botToken" class="input mono" spellcheck="false" autocomplete="off" placeholder="8123456789:AAF..." />
        </label>
        <p class="faint small">
          Создайте бота командой <code>/newbot</code> в <a href="https://t.me/BotFather" target="_blank" rel="noopener">@BotFather</a>.
          Название и username мы прочитаем из Telegram сами.
        </p>
      </div>

      <p v-if="error" class="error-banner">{{ error }}</p>
      <p v-else-if="done" class="ok">{{ done }}</p>

      <div class="actions">
        <button class="btn btn-primary" :disabled="busy || (!changed() && !(choice === 'Telegram' && !!info.bot))" @click="save">
          {{ busy ? 'Проверяем…' : changed() ? 'Сохранить' : 'Привязать заново' }}
        </button>
      </div>
    </template>
  </section>
</template>

<style scoped>
.choices { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 10px; }
.choices label {
  display: grid; grid-template-columns: auto 1fr; gap: 2px 10px; align-items: center; cursor: pointer;
  border: 1px solid var(--border-strong); border-radius: 10px; padding: 11px 14px;
}
.choices label.on { border-color: var(--plum-600); background: var(--plum-50); }
.choices input { grid-row: span 2; width: 17px; height: 17px; accent-color: var(--plum-600); margin: 0; }
.choices b { font-weight: 600; }
.choices small { color: var(--text-2); font-size: 12.5px; line-height: 1.4; }

.link { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; margin-top: 14px; }
.link code { background: var(--surface-2); border: 1px solid var(--border); border-radius: 8px; padding: 5px 9px; font-size: 12.5px; }

.tg { margin-top: 14px; display: flex; flex-direction: column; gap: 10px; }
.bot-row { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.bot-row div { display: flex; gap: 8px; align-items: baseline; }
.bot-row a { font-size: 13px; }
.badge.good { background: var(--good-bg); color: var(--good); }
.badge.warn { background: var(--warn-bg); color: var(--warn); }
.hint { margin: 0; font-size: 13px; color: var(--text-2); line-height: 1.45; }
.hint b { color: var(--text); }
.warning code { background: rgb(255 255 255 / 55%); border-radius: 5px; padding: 1px 5px; font-size: 12px; }
.warning { margin: 0; padding: 9px 11px; border-radius: 8px; background: var(--warn-bg); color: var(--warn); font-size: 13px; line-height: 1.45; }
.mono { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 13px; }
.small { font-size: 12.5px; line-height: 1.45; margin: 0; }
.small code { background: var(--surface-2); border-radius: 5px; padding: 1px 5px; }
.ok { margin: 12px 0 0; color: var(--good); font-size: 13px; font-weight: 500; }
.actions { margin-top: 14px; }
</style>
