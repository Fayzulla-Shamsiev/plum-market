<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { marketingApi, type ChannelState } from '../../api'
import Icon from '../../components/Icon.vue'
import SingleImage from '../../components/SingleImage.vue'
import TelegramPreview from '../../components/TelegramPreview.vue'
import { dateTime } from '../../format'

const state = ref<ChannelState | null>(null)
const channelInput = ref('')
const connectError = ref('')
const checking = ref(false)

const text = ref('')
const imageUrl = ref<string | null>(null)
const withButton = ref(true)
const buttonText = ref('Заказать')
const buttonUrl = ref('')
const publishing = ref(false)
const publishError = ref('')
const published = ref(false)

async function load() {
  state.value = await marketingApi.channel()
  if (!buttonUrl.value) buttonUrl.value = `https://t.me/${state.value.botUsername}`
}
onMounted(load)

const connected = computed(() => !!state.value?.channel)

// Steps from the spec; the first one is already done because every store gets its own bot.
const steps = computed(() => [
  { title: 'Создайте Telegram-бота', body: `Бот вашего магазина уже создан: @${state.value?.botUsername ?? '…'}.`, done: true },
  { title: 'Откройте настройки канала', body: 'В Telegram откройте свой канал → нажмите на название → «Изменить» (карандаш).', done: connected.value },
  { title: 'Добавьте бота в администраторы', body: `Раздел «Администраторы» → «Добавить администратора» → найдите @${state.value?.botUsername ?? 'бота'}.`, done: connected.value },
  { title: 'Дайте право публиковать сообщения', body: 'В правах администратора включите «Публикация сообщений» и сохраните.', done: connected.value },
  { title: 'Опубликуйте первый пост', body: 'Укажите канал ниже, проверьте подключение и отправьте пост.', done: (state.value?.posts.length ?? 0) > 0 },
])

async function connect() {
  checking.value = true
  connectError.value = ''
  try {
    await marketingApi.connectChannel(channelInput.value)
    await load()
  } catch (e) {
    connectError.value = (e as Error).message
  } finally {
    checking.value = false
  }
}
async function disconnect() {
  if (!confirm('Отключить канал?')) return
  await marketingApi.disconnectChannel()
  load()
}

async function publish() {
  publishing.value = true
  publishError.value = ''
  try {
    await marketingApi.publishPost({
      text: text.value, imageUrl: imageUrl.value,
      buttonText: withButton.value ? buttonText.value : null, buttonUrl: withButton.value ? buttonUrl.value : null,
    })
    text.value = ''
    imageUrl.value = null
    published.value = true
    setTimeout(() => (published.value = false), 3000)
    load()
  } catch (e) {
    publishError.value = (e as Error).message
  } finally {
    publishing.value = false
  }
}
</script>

<template>
  <div class="page narrow">
    <div class="page-head"><h1>Пост для канала</h1></div>
    <div v-if="!state" class="skeleton" style="height: 400px" />
    <template v-else>
      <section class="card card-pad">
        <h2>Подключение бота к каналу</h2>
        <ol class="steps">
          <li v-for="(s, i) in steps" :key="i" :class="{ done: s.done }">
            <span class="n">{{ s.done ? '✓' : i + 1 }}</span>
            <div><b>{{ s.title }}</b><p>{{ s.body }}</p></div>
          </li>
        </ol>

        <div v-if="!connected" class="connect">
          <label class="field grow"><span>Канал</span>
            <input v-model="channelInput" class="input" placeholder="@my_channel или https://t.me/my_channel" @keydown.enter="connect" />
          </label>
          <button class="btn btn-primary" :disabled="!channelInput.trim() || checking" @click="connect">{{ checking ? 'Проверяю…' : 'Проверить подключение' }}</button>
        </div>
        <div v-else class="connected">
          <span class="badge Completed">Подключён</span>
          <b>{{ state.channel }}</b>
          <span class="faint small">с {{ dateTime(state.connectedAt!) }}</span>
          <button class="btn btn-sm btn-ghost btn-danger" @click="disconnect">Отключить</button>
        </div>
        <p v-if="connectError" class="err">{{ connectError }}</p>
        <p class="faint small note">В прототипе проверяется только формат имени канала; в рабочей версии бот запросит свои права в канале через Telegram Bot API.</p>
      </section>

      <div v-if="connected" class="compose">
        <section class="card card-pad form">
          <h2>Новый пост в {{ state.channel }}</h2>
          <div class="field"><span>Фото</span><SingleImage v-model="imageUrl" label="Добавить фото" wide /></div>
          <label class="field">
            <span class="row" style="justify-content: space-between">Текст
              <em class="faint" :class="{ over: text.length > (imageUrl ? 1024 : 4096) }">{{ text.length }} / {{ imageUrl ? 1024 : 4096 }}</em></span>
            <textarea v-model="text" class="textarea" rows="7" placeholder="Расскажите о новинке, акции или событии…" />
          </label>
          <label class="switch"><input v-model="withButton" type="checkbox" /><span class="track" />Кнопка под постом</label>
          <div v-if="withButton" class="grid2">
            <label class="field"><span>Текст кнопки</span><input v-model="buttonText" class="input" maxlength="40" /></label>
            <label class="field"><span>Ссылка</span><input v-model="buttonUrl" class="input" /></label>
          </div>
          <div v-if="publishError" class="error-banner">{{ publishError }}</div>
          <div class="actions">
            <span v-if="published" class="ok">✓ Опубликовано</span>
            <button class="btn btn-primary" :disabled="!text.trim() || publishing" @click="publish"><Icon name="send" />Опубликовать</button>
          </div>
        </section>
        <aside class="preview">
          <span class="faint small">Предпросмотр</span>
          <TelegramPreview :title="state.channel!.replace('@', '')" :text="text" :image-url="imageUrl" :button-text="withButton ? buttonText : null" channel />
        </aside>
      </div>

      <section v-if="state.posts.length" class="card card-pad">
        <h2>Опубликованные посты</h2>
        <ul class="posts">
          <li v-for="p in state.posts" :key="p.id">
            <img v-if="p.imageUrl" :src="p.imageUrl" alt="" />
            <div class="grow"><div class="clamp">{{ p.text }}</div>
              <span class="faint small">{{ p.channel }} · {{ dateTime(p.publishedAt) }}<template v-if="p.buttonText"> · кнопка «{{ p.buttonText }}»</template></span></div>
          </li>
        </ul>
      </section>
    </template>
  </div>
</template>

<style scoped>
.narrow { max-width: 1040px; display: flex; flex-direction: column; gap: 16px; }
.page-head { margin-bottom: 0; }
.card h2 { margin-bottom: 14px; }
.steps { list-style: none; margin: 0 0 16px; padding: 0; display: flex; flex-direction: column; gap: 12px; }
.steps li { display: flex; gap: 12px; }
.steps .n { flex: none; width: 28px; height: 28px; border-radius: 50%; display: grid; place-items: center; background: var(--plum-100); color: var(--plum-700); font-weight: 700; font-size: 13px; }
.steps .done .n { background: var(--good); color: #fff; }
.steps p { margin: 2px 0 0; color: var(--text-2); font-size: 13px; }
.connect { display: flex; gap: 10px; align-items: flex-end; }
.connected { display: flex; gap: 10px; align-items: center; flex-wrap: wrap; padding: 10px 12px; border-radius: 10px; background: var(--good-bg); }
.err { color: var(--bad); font-size: 13px; margin: 8px 0 0; }
.note { margin: 10px 0 0; }
.compose { display: grid; grid-template-columns: minmax(0, 1fr) 340px; gap: 16px; align-items: start; }
.form { display: flex; flex-direction: column; gap: 14px; }
.form h2 { margin: 0; }
.preview { position: sticky; top: 16px; display: flex; flex-direction: column; gap: 8px; }
.grid2 { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
.actions { display: flex; justify-content: flex-end; align-items: center; gap: 12px; }
.ok { color: var(--good); font-weight: 600; }
em { font-style: normal; font-weight: 400; font-size: 12px; }
em.over { color: var(--bad); font-weight: 600; }
.posts { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 10px; }
.posts li { display: flex; gap: 12px; padding: 10px; border: 1px solid var(--border); border-radius: 10px; }
.posts img { width: 64px; height: 64px; object-fit: cover; border-radius: 8px; }
.clamp { display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
.small { font-size: 12px; }
@media (max-width: 900px) { .compose { grid-template-columns: 1fr; } .preview { position: static; } .connect { flex-direction: column; align-items: stretch; } }
</style>
