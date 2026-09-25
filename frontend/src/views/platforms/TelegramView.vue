<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { api, type Platforms } from '../../api'
import AutoReplyEditor from '../../components/AutoReplyEditor.vue'
import Icon from '../../components/Icon.vue'

// Платформы → Telegram-бот: the merchant's own bot from @BotFather becomes a second door into the same shop.
// Connecting it points the bot's menu button at the storefront, which is the "Open Shop" button customers press.
const data = ref<Platforms | null>(null)
const botToken = ref('')
const busy = ref('')
const error = ref('')
const done = ref('')

onMounted(() => api.platforms().then(fill))

// The bot's own wording: what an empty chat shows, and how it answers /start.
type Tab = 'bot' | 'messages' | 'autoreply'
const tab = ref<Tab>('bot')
const about = ref('')
const greeting = ref('')
const aiBusy = ref<'' | 'about' | 'greeting'>('')
const autoreply = ref<InstanceType<typeof AutoReplyEditor> | null>(null)
const dirty = computed(() =>
  !!data.value?.telegram && (about.value !== data.value.telegram.about || greeting.value !== data.value.telegram.greeting))

function fill(p: Platforms) {
  data.value = p
  about.value = p.telegram?.about ?? ''
  greeting.value = p.telegram?.greeting ?? ''
}
// A bot that isn't connected has nothing to say yet.
watch(() => data.value?.telegram, t => { if (!t) tab.value = 'bot' })

async function saveMessages() {
  if (busy.value) return
  busy.value = 'messages'
  error.value = ''
  done.value = ''
  try {
    fill(await api.saveBotMessages({ about: about.value.trim(), greeting: greeting.value.trim() }))
    done.value = 'Тексты обновлены — бот уже отвечает по-новому.'
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busy.value = ''
  }
}

/** Lets the model write the text; the administrator still edits and presses «Обновить». */
async function generate(kind: 'about' | 'greeting') {
  if (aiBusy.value) return
  aiBusy.value = kind
  error.value = ''
  done.value = ''
  try {
    const target = kind === 'about' ? about : greeting
    const { text } = await api.botText(kind, target.value.trim() || undefined)
    target.value = text
    done.value = 'Текст сгенерирован — проверьте и нажмите «Обновить».'
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    aiBusy.value = ''
  }
}

async function run(action: 'connect' | 'disconnect') {
  if (busy.value) return
  busy.value = action
  error.value = ''
  done.value = ''
  try {
    if (action === 'disconnect') {
      fill(await api.disconnectBot())
      done.value = 'Бот отключён. Магазин по-прежнему открыт на сайте.'
    } else {
      const had = !!data.value?.telegram
      fill(await api.connectBot(botToken.value.trim() || undefined))
      botToken.value = ''
      done.value = data.value?.telegram?.linkedAt
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
      <div class="seg" role="tablist">
        <button role="tab" :aria-selected="tab === 'bot'" :class="{ on: tab === 'bot' }" @click="tab = 'bot'">Бот</button>
        <button role="tab" :aria-selected="tab === 'messages'" :class="{ on: tab === 'messages' }" @click="tab = 'messages'">Сообщения бота</button>
        <button role="tab" :aria-selected="tab === 'autoreply'" :class="{ on: tab === 'autoreply' }" @click="tab = 'autoreply'">Автоответчик</button>
      </div>

      <!-- ---------------- Бот: подключение ---------------- -->
      <template v-if="tab === 'bot'">
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
            <li><b>Статусы заказов.</b> Когда покупатель открыл магазин в боте, обновления по его заказу приходят и туда.</li>
          </ul>

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

      <!-- ---------------- Сообщения бота ---------------- -->
      <template v-else-if="tab === 'messages'">
        <section class="card card-pad">
          <div class="card-head">
            <h2>До нажатия «Начать»</h2>
            <span class="card-sub">описание бота в пустом чате</span>
            <button class="btn btn-sm" :disabled="!!aiBusy" @click="generate('about')">
              <Icon name="sparkles" />{{ aiBusy === 'about' ? 'Пишем…' : 'Сгенерировать' }}
            </button>
          </div>
          <textarea v-model="about" class="input area" rows="3" maxlength="500" />
          <p class="faint small">Это первое, что видит покупатель, открыв бота. До 400 символов.</p>
        </section>

        <section class="card card-pad">
          <div class="card-head">
            <h2>Приветствие после «Начать»</h2>
            <span class="card-sub">ответ бота на /start</span>
            <button class="btn btn-sm" :disabled="!!aiBusy" @click="generate('greeting')">
              <Icon name="sparkles" />{{ aiBusy === 'greeting' ? 'Пишем…' : 'Сгенерировать' }}
            </button>
          </div>
          <textarea v-model="greeting" class="input area" rows="4" maxlength="1000" />
          <p class="faint small">
            <code>{name}</code> — имя покупателя, <code>{store}</code> — название магазина.
            Кнопка «Открыть магазин» добавляется к сообщению сама.
          </p>
          <div class="preview">{{ greeting.replace('{name}', 'Малика').replace('{store}', data.website.name) }}</div>
        </section>

        <div class="actions">
          <button class="btn btn-primary" :disabled="busy === 'messages' || !dirty" @click="saveMessages">
            {{ busy === 'messages' ? 'Обновляем…' : 'Обновить' }}
          </button>
        </div>
      </template>

      <!-- ---------------- Автоответчик ---------------- -->
      <template v-else>
        <section class="card card-pad">
          <div class="card-head">
            <h2>Сообщения о заказе</h2>
            <span class="card-sub">на каждом шаге — в чат магазина и в Telegram</span>
          </div>
          <p class="faint small intro">
            Покупатель получает сообщение при смене статуса своего заказа на своём языке. В Telegram оно приходит,
            если покупатель открыл магазин в этом боте.
          </p>
          <AutoReplyEditor ref="autoreply" @saved="done = 'Тексты сообщений сохранены.'" />
          <div class="actions">
            <button class="btn btn-primary" @click="autoreply?.save()">Сохранить</button>
          </div>
        </section>
      </template>
    </template>

    <section v-else class="card card-pad">
      <div class="card-head"><h2>Подключите бота</h2><span class="card-sub">магазин откроется внутри Telegram</span></div>
      <p class="lead">
        Покупатель напишет боту «Начать» — тот поздоровается и покажет кнопку, которая открывает ваш магазин
        прямо в Telegram: каталог, корзина и оформление заказа.
      </p>
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
.seg { display: inline-flex; gap: 2px; padding: 3px; margin-bottom: 16px; background: var(--surface-2); border: 1px solid var(--border); border-radius: 9px; }
.seg button { height: 30px; padding: 0 14px; border: 0; border-radius: 7px; background: none; font: inherit; font-size: 13.5px; font-weight: 550; color: var(--text-2); cursor: pointer; }
.seg button.on { background: var(--surface); color: var(--text); box-shadow: var(--shadow); }
.card-head .btn { margin-left: auto; }
.area { width: 100%; height: auto; padding: 10px 12px; line-height: 1.5; resize: vertical; }
.small code { background: var(--surface-2); border-radius: 5px; padding: 1px 5px; }
.intro { margin: 0 0 14px; }
.preview { margin-top: 12px; background: #e7f3fd; border-radius: 12px 12px 12px 3px; padding: 9px 12px; font-size: 13.5px; line-height: 1.5; max-width: 90%; white-space: pre-wrap; }
.ok { margin: 14px 0 0; color: var(--good); font-size: 13px; font-weight: 500; }
.page > .card + .card { margin-top: 16px; }
</style>
