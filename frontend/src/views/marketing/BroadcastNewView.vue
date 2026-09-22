<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { marketingApi, type AudienceFilter } from '../../api'
import AudiencePicker from '../../components/AudiencePicker.vue'
import Icon from '../../components/Icon.vue'
import SingleImage from '../../components/SingleImage.vue'
import TelegramPreview from '../../components/TelegramPreview.vue'
import { count } from '../../format'
import { useLookups } from '../../store'

const router = useRouter()
const { lookups } = useLookups()

// Spec: first choose recipients, then the message form opens.
const step = ref<1 | 2>(1)
const audience = ref<AudienceFilter>({})
const stats = ref({ total: 0, reachable: 0 })

const name = ref('')
const text = ref('')
const imageUrl = ref<string | null>(null)
const withButton = ref(false)
const buttonText = ref('')
const buttonUrl = ref('')
const when = ref<'now' | 'later'>('now')
const sendAt = ref('')
const sending = ref(false)
const error = ref('')

const limit = computed(() => (imageUrl.value ? 1024 : 4096))
const canSend = computed(() => name.value.trim() && text.value.trim() && text.value.length <= limit.value
  && (!withButton.value || (buttonText.value.trim() && /^https?:\/\/\S+$/.test(buttonUrl.value.trim())))
  && (when.value === 'now' || sendAt.value))

async function send() {
  sending.value = true
  error.value = ''
  try {
    await marketingApi.createBroadcast({
      name: name.value, text: text.value, imageUrl: imageUrl.value,
      buttonText: withButton.value ? buttonText.value : null, buttonUrl: withButton.value ? buttonUrl.value : null,
      sendAt: when.value === 'later' ? sendAt.value : null, audience: audience.value,
    })
    router.push('/marketing/broadcasts')
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    sending.value = false
  }
}
</script>

<template>
  <div class="page narrow">
    <div class="page-head">
      <RouterLink to="/marketing/broadcasts" class="btn btn-ghost btn-icon" aria-label="Назад"><Icon name="chevronLeft" /></RouterLink>
      <h1>Новая рассылка</h1>
    </div>
    <ol class="steps">
      <li :class="{ on: step === 1, done: step > 1 }"><span>1</span>Получатели</li>
      <li :class="{ on: step === 2 }"><span>2</span>Сообщение</li>
    </ol>

    <section v-if="step === 1" class="card card-pad">
      <h2>Кому отправить</h2>
      <AudiencePicker v-model="audience" v-model:stats="stats" channel="bot"
                      reach-hint="Рассылка уходит через Telegram-бот: клиенты с сайта и из Instagram её не получат." />
      <div class="actions">
        <RouterLink to="/marketing/broadcasts" class="btn">Отмена</RouterLink>
        <button class="btn btn-primary" :disabled="!stats.reachable" @click="step = 2">Далее<Icon name="chevronRight" /></button>
      </div>
    </section>

    <div v-else class="compose">
      <section class="card card-pad form">
        <h2>Сообщение <span class="faint small">· получат {{ count(stats.reachable) }}</span></h2>
        <label class="field"><span>Название рассылки *</span><input v-model="name" class="input" placeholder="Видно только вам, например: Скидка на кофе" /></label>
        <div class="field"><span>Изображение</span><SingleImage v-model="imageUrl" label="Добавить картинку" wide /></div>
        <label class="field">
          <span class="row" style="justify-content: space-between">Текст *<em class="faint" :class="{ over: text.length > limit }">{{ text.length }} / {{ limit }}</em></span>
          <textarea v-model="text" class="textarea" rows="6" placeholder="Текст сообщения. Можно использовать эмодзи 🎉" />
        </label>
        <label class="switch"><input v-model="withButton" type="checkbox" /><span class="track" />Добавить кнопку со ссылкой</label>
        <div v-if="withButton" class="grid2">
          <label class="field"><span>Текст кнопки</span><input v-model="buttonText" class="input" maxlength="40" placeholder="Заказать" /></label>
          <label class="field"><span>Ссылка</span><input v-model="buttonUrl" class="input" placeholder="https://plum-bakery.plum.uz/menu" /></label>
        </div>
        <div class="field">
          <span>Когда отправить</span>
          <div class="row">
            <label class="radio"><input v-model="when" type="radio" value="now" />Сейчас</label>
            <label class="radio"><input v-model="when" type="radio" value="later" />Запланировать</label>
            <input v-if="when === 'later'" v-model="sendAt" type="datetime-local" class="input" aria-label="Дата и время отправки" />
          </div>
        </div>
        <div v-if="error" class="error-banner">{{ error }}</div>
        <div class="actions">
          <button class="btn" @click="step = 1"><Icon name="chevronLeft" />Назад</button>
          <button class="btn btn-primary" :disabled="!canSend || sending" @click="send">
            <Icon name="send" />{{ when === 'now' ? `Отправить ${count(stats.reachable)} клиентам` : 'Запланировать' }}
          </button>
        </div>
      </section>
      <aside class="preview">
        <span class="faint small">Предпросмотр</span>
        <TelegramPreview :title="lookups?.store.storeName ?? 'Бот'" :text="text" :image-url="imageUrl" :button-text="withButton ? buttonText || 'Кнопка' : null" />
      </aside>
    </div>
  </div>
</template>

<style scoped>
.narrow { max-width: 1040px; }
.steps { list-style: none; display: flex; gap: 24px; padding: 0; margin: 0 0 16px; }
.steps li { display: flex; align-items: center; gap: 8px; color: var(--text-3); font-weight: 600; }
.steps span { width: 26px; height: 26px; border-radius: 50%; display: grid; place-items: center; background: var(--border); color: var(--text-2); font-size: 13px; }
.steps .on { color: var(--text); }
.steps .on span { background: var(--plum-600); color: #fff; }
.steps .done span { background: var(--good); color: #fff; }
.card h2 { margin-bottom: 14px; }
.compose { display: grid; grid-template-columns: minmax(0, 1fr) 340px; gap: 16px; align-items: start; }
.form { display: flex; flex-direction: column; gap: 14px; }
.form h2 { margin: 0; }
.preview { position: sticky; top: 16px; display: flex; flex-direction: column; gap: 8px; }
.grid2 { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
.radio { display: inline-flex; gap: 6px; align-items: center; cursor: pointer; }
.radio input { accent-color: var(--plum-600); }
.actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 16px; }
.form .actions { margin-top: 0; }
em { font-style: normal; font-weight: 400; font-size: 12px; }
em.over { color: var(--bad); font-weight: 600; }
.small { font-size: 12px; font-weight: 400; }
@media (max-width: 900px) { .compose { grid-template-columns: 1fr; } .preview { position: static; } }
</style>
