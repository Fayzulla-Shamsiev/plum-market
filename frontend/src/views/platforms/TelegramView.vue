<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { api, type Platforms } from '../../api'
import Icon from '../../components/Icon.vue'

// Платформы → Telegram-бот: the merchant's own bot from @BotFather becomes a second door into the same shop.
// Connecting it points the bot's menu button at the storefront, which is the "Open Shop" button customers press.
const data = ref<Platforms | null>(null)
const botToken = ref('')
const busy = ref('')
const error = ref('')
const done = ref('')

onMounted(() => api.platforms().then(p => (data.value = p)))

async function run(action: 'connect' | 'disconnect') {
  if (busy.value) return
  busy.value = action
  error.value = ''
  done.value = ''
  try {
    if (action === 'disconnect') {
      data.value = await api.disconnectBot()
      done.value = 'Бот отключён. Магазин по-прежнему открыт на сайте.'
    } else {
      const had = !!data.value?.telegram
      data.value = await api.connectBot(botToken.value.trim() || undefined)
      botToken.value = ''
      done.value = data.value.telegram?.linkedAt
        ? had ? 'Кнопка «Open Shop» обновлена.' : 'Бот подключён — магазин открывается прямо в Telegram.'
        : 'Бот подключён.'
    }
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busy.value = ''
  }
}
</script>

<template>
  <div class="page narrow">
    <div class="page-head">
      <h1>Telegram-бот</h1>
      <a v-if="data?.telegram" class="btn btn-primary" :href="data.telegram.url" target="_blank" rel="noopener">
        <Icon name="bot" />Открыть бота
      </a>
    </div>

    <div v-if="!data" class="skeleton" style="height: 320px" />

    <template v-else-if="data.telegram">
      <section class="card card-pad">
        <div class="card-head"><h2>Бот подключён</h2><span class="card-sub">магазин открывается внутри Telegram</span></div>
        <div class="bot">
          <div class="who">
            <b>{{ data.telegram.name }}</b>
            <a :href="data.telegram.url" target="_blank" rel="noopener">@{{ data.telegram.username }}</a>
          </div>
          <span v-if="data.telegram.linkedAt" class="badge good">Кнопка «Open Shop» подключена</span>
          <span v-else class="badge warn">Кнопка не подключена</span>
        </div>

        <ul class="what">
          <li><b>Приветствие.</b> Покупатель нажимает «Начать» — бот здоровается и сразу показывает кнопку «Открыть магазин», искать ничего не нужно.</li>
          <li><b>Кнопка меню.</b> Слева от поля ввода появляется «Open Shop» — тот же магазин, что и на сайте.</li>
        </ul>
        <p v-if="!data.telegram.greets" class="hint">Приветствие включится, когда магазин будет опубликован.</p>

        <div class="actions">
          <button class="btn" :disabled="!!busy" @click="run('connect')">
            {{ busy === 'connect' ? 'Проверяем…' : 'Привязать заново' }}
          </button>
          <button class="btn btn-danger" :disabled="!!busy" @click="run('disconnect')">
            {{ busy === 'disconnect' ? 'Отключаем…' : 'Отключить бота' }}
          </button>
        </div>
      </section>

      <section class="card card-pad">
        <div class="card-head"><h2>Другой бот</h2><span class="card-sub">текущий бот перестанет открывать магазин</span></div>
        <label class="field">
          <span>Токен из @BotFather</span>
          <input v-model="botToken" class="input mono" spellcheck="false" autocomplete="off" placeholder="8123456789:AAF..." />
        </label>
        <div class="actions">
          <button class="btn btn-primary" :disabled="!botToken.trim() || !!busy" @click="run('connect')">Подключить</button>
        </div>
      </section>
    </template>

    <section v-else class="card card-pad">
      <div class="card-head"><h2>Подключите бота</h2><span class="card-sub">магазин откроется внутри Telegram</span></div>
      <ol class="steps">
        <li>Откройте <a href="https://t.me/BotFather" target="_blank" rel="noopener">@BotFather</a> и отправьте команду <code>/newbot</code>.</li>
        <li>Укажите название бота и его username — он должен заканчиваться на <code>bot</code>.</li>
        <li>Скопируйте токен из ответа и вставьте его сюда.</li>
      </ol>
      <label class="field">
        <span>Токен бота</span>
        <input v-model="botToken" class="input mono" spellcheck="false" autocomplete="off" placeholder="8123456789:AAF..." />
        <small class="faint">Название и username бота подставятся сами — мы проверим токен в Telegram.</small>
      </label>
      <div class="actions">
        <button class="btn btn-primary" :disabled="!botToken.trim() || !!busy" @click="run('connect')">
          {{ busy ? 'Проверяем…' : 'Подключить бота' }}
        </button>
      </div>
    </section>

    <p v-if="error" class="error-banner">{{ error }}</p>
    <p v-else-if="done" class="ok">{{ done }}</p>
  </div>
</template>

<style scoped>
.bot { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
.who { display: flex; gap: 8px; align-items: baseline; }
.who b { font-size: 15px; }
.who a { font-size: 13px; }
.badge.good { background: var(--good-bg); color: var(--good); }
.badge.warn { background: var(--warn-bg); color: var(--warn); }
.what { margin: 12px 0 0; padding-left: 18px; display: grid; gap: 6px; color: var(--text-2); font-size: 13.5px; line-height: 1.5; }
.what b { color: var(--text); font-weight: 600; }
.lead { margin: 0 0 16px; color: var(--text-2); font-size: 13.5px; line-height: 1.5; }
.hint { margin: 12px 0 0; color: var(--text-2); font-size: 13.5px; line-height: 1.5; }
.hint b { color: var(--text); }
.warning { margin: 12px 0 0; padding: 10px 12px; border-radius: 8px; background: var(--warn-bg); color: var(--warn); font-size: 13px; line-height: 1.5; }
.warning code { background: rgb(255 255 255 / 55%); border-radius: 5px; padding: 1px 5px; font-size: 12px; }
.steps { margin: 0 0 16px; padding-left: 20px; display: grid; gap: 6px; color: var(--text-2); font-size: 13.5px; line-height: 1.5; }
.steps code { background: var(--surface-2); border-radius: 5px; padding: 1px 5px; font-size: 12.5px; }
.mono { font-family: ui-monospace, SFMono-Regular, Menlo, monospace; font-size: 13px; }
.actions { margin-top: 16px; display: flex; gap: 8px; flex-wrap: wrap; }
.ok { margin: 14px 0 0; color: var(--good); font-size: 13px; font-weight: 500; }
.page > .card + .card { margin-top: 16px; }
</style>
