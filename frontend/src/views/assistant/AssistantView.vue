<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { api, catalogApi, type AssistantCard, type AssistantMessage, type SetupStep } from '../../api'
import { admin } from '../../auth'
import Icon from '../../components/Icon.vue'
import { refreshLookups } from '../../store'
import AvailabilityCard from './AvailabilityCard.vue'
import CategoriesCard from './CategoriesCard.vue'
import TelegramConnectCard from './TelegramConnectCard.vue'

// ИИ-помощник. A new administrator lands here after registration instead of an empty panel: the assistant asks
// about the business and fills the shop in (description, catalog, branch, contacts, delivery, return terms,
// Telegram bot), while the list on the right shows what is left. Everything it changes is ordinary shop data,
// editable in the panel afterwards.
const router = useRouter()

const messages = ref<AssistantMessage[]>([])
const progress = ref<SetupStep[]>([])
const enabled = ref(true)
const onboarded = ref(false)
const storeName = ref(admin.value?.store.name ?? '')
const loading = ref(true)
const loadError = ref('')
const busy = ref(false)
const sendError = ref('')
const text = ref('')
const attachments = ref<string[]>([])
const uploading = ref(false)
const showSteps = ref(false)

const feedEl = ref<HTMLElement>()
const inputEl = ref<HTMLTextAreaElement>()
const fileEl = ref<HTMLInputElement>()

const shopHref = computed(() => (admin.value ? `/shop/${admin.value.store.slug}` : '/'))
const required = computed(() => progress.value.filter(s => !s.optional))
const doneCount = computed(() => required.value.filter(s => s.done).length)
const percent = computed(() => (required.value.length ? Math.round((doneCount.value / required.value.length) * 100) : 0))
const botUsername = ref<string | null>(null)
const telegramDone = computed(() => progress.value.find(s => s.key === 'telegram')?.done ?? false)
const last = computed(() => messages.value[messages.value.length - 1])
const suggestions = computed(() => (!busy.value && last.value?.role === 'assistant' ? last.value.suggestions : []))

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    const s = await api.assistant()
    messages.value = s.messages
    progress.value = s.progress
    enabled.value = s.enabled
    onboarded.value = s.onboarded
    storeName.value = s.storeName
    scrollDown()
  } catch (e) {
    loadError.value = (e as Error).message
  } finally {
    loading.value = false
  }
}
onMounted(load)

function scrollDown(smooth = false) {
  nextTick(() => feedEl.value?.scrollTo({ top: feedEl.value.scrollHeight, behavior: smooth ? 'smooth' : 'auto' }))
}

// ---- sending ----

/** A Telegram bot token («123456789:AA…») must never reach the chat or the AI. */
const BOT_TOKEN = /\b\d{6,12}:[A-Za-z0-9_-]{30,}\b/

async function send(raw?: string) {
  if (busy.value || uploading.value) return
  let body = (raw ?? text.value).trim()
  const files = raw === undefined ? [...attachments.value] : []
  if (!body && files.length === 0) return
  sendError.value = ''
  busy.value = true

  // Pasted by mistake: connect the bot straight from here and tell the assistant only the outcome.
  const token = BOT_TOKEN.exec(body)?.[0]
  if (token) {
    body = body.replace(token, '[токен скрыт]')
    try {
      const p = await api.connectBot(token)
      if (p.telegram) botUsername.value = p.telegram.username
      body += `\n\n(Бот @${p.telegram?.username} подключён через защищённую форму.)`
    } catch (e) {
      body += `\n\n(Подключить бота не удалось: ${(e as Error).message})`
    }
  }

  // Shown right away; replaced by the server's copy when the answer arrives.
  const pending: AssistantMessage = {
    id: -Date.now(), role: 'user', text: body, suggestions: [], cards: [], attachments: files, createdAt: new Date().toISOString(),
  }
  messages.value.push(pending)
  if (raw === undefined) {
    text.value = ''
    attachments.value = []
    nextTick(autosize)
  }
  scrollDown(true)

  try {
    const turn = await api.assistantSend(body, files)
    messages.value = [...messages.value.filter(m => m.id !== pending.id), ...turn.messages]
    progress.value = turn.progress
    storeName.value = turn.storeName
    if (turn.onboarded && !onboarded.value) {
      onboarded.value = true
      if (admin.value) admin.value.store.onboarded = true
    }
    if (admin.value) admin.value.store.name = turn.storeName
    // Branches or the name may have changed: the panel's branch picker must know about them.
    refreshLookups()
    scrollDown(true)
  } catch (e) {
    messages.value = messages.value.filter(m => m.id !== pending.id)
    if (raw === undefined) {
      text.value = body
      attachments.value = files
    }
    sendError.value = (e as Error).message
  } finally {
    busy.value = false
    nextTick(() => inputEl.value?.focus())
  }
}

function onKey(e: KeyboardEvent) {
  if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) {
    e.preventDefault()
    send()
  }
}

function autosize() {
  const el = inputEl.value
  if (!el) return
  el.style.height = 'auto'
  el.style.height = `${Math.min(el.scrollHeight, 168)}px`
}

async function attach(e: Event) {
  const input = e.target as HTMLInputElement
  const files = [...(input.files ?? [])].slice(0, 8 - attachments.value.length)
  input.value = ''
  if (!files.length) return
  uploading.value = true
  sendError.value = ''
  try {
    for (const f of files) {
      if (!f.type.startsWith('image/')) throw new Error('Можно прикрепить только фото')
      attachments.value.push((await catalogApi.upload(f)).url)
    }
  } catch (err) {
    sendError.value = (err as Error).message
  } finally {
    uploading.value = false
  }
}

function askAbout(step: SetupStep) {
  showSteps.value = false
  send(`Давайте настроим: ${step.title.toLowerCase()}`)
}

function botConnected(username: string) {
  botUsername.value = username
  send(`Я подключил Telegram-бота @${username}.`)
}

// ---- leaving ----

async function toPanel() {
  try {
    if (!onboarded.value) await api.assistantFinish()
    if (admin.value) admin.value.store.onboarded = true
  } catch { /* the panel opens either way */ }
  router.push('/dashboard')
}

async function reset() {
  if (busy.value || !confirm('Начать разговор заново? Всё, что уже настроено в магазине, останется.')) return
  const s = await api.assistantReset()
  messages.value = s.messages
  progress.value = s.progress
}

// ---- «Думаю…» while the assistant works: a turn can take several model rounds ----
const statuses = ['Думаю…', 'Смотрю настройки магазина…', 'Заполняю магазин…', 'Проверяю, что получилось…']
const status = ref(statuses[0])
let timer: ReturnType<typeof setInterval> | undefined
watch(busy, on => {
  clearInterval(timer)
  status.value = statuses[0]
  if (on) {
    let i = 0
    timer = setInterval(() => { status.value = statuses[Math.min(++i, statuses.length - 1)] }, 2600)
  }
})
onBeforeUnmount(() => clearInterval(timer))

// ---- the assistant's text: a small, escaped subset of markdown (bold, lists, links) ----

function escape(s: string) {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;')
}
function inline(s: string) {
  return escape(s)
    .replace(/\*\*(.+?)\*\*/g, '<b>$1</b>')
    .replace(/\[([^\]]+)\]\((\/[^)\s]*|https?:\/\/[^)\s]+)\)/g, (_, label: string, href: string) =>
      // Panel pages open in place; the shop itself (and anything outside) in a new tab, so the chat stays open.
      href.startsWith('/') && !href.startsWith('/shop/')
        ? `<a href="${href}" data-internal>${label}</a>`
        : `<a href="${href}" target="_blank" rel="noopener">${label}</a>`)
    .replace(/(^|[\s(«])(https?:\/\/[^\s<)»,]+[^\s<)».,!?:;])/g, '$1<a href="$2" target="_blank" rel="noopener">$2</a>')
}
function render(md: string) {
  const out: string[] = []
  let list: { tag: 'ul' | 'ol'; items: string[] } | null = null
  let para: string[] = []
  const flushPara = () => { if (para.length) out.push(`<p>${para.join('<br>')}</p>`); para = [] }
  const flushList = () => { if (list) out.push(`<${list.tag}>${list.items.map(i => `<li>${i}</li>`).join('')}</${list.tag}>`); list = null }
  for (const line of md.split('\n')) {
    const bullet = /^\s*[-•*]\s+(.*)$/.exec(line)
    const numbered = /^\s*\d+[.)]\s+(.*)$/.exec(line)
    if (bullet || numbered) {
      flushPara()
      const tag = bullet ? 'ul' : 'ol'
      if (list && list.tag !== tag) flushList()
      list ??= { tag, items: [] }
      list.items.push(inline((bullet ?? numbered)![1]))
    } else if (!line.trim()) {
      flushPara()
      flushList()
    } else {
      flushList()
      const heading = /^#+\s*(.*)$/.exec(line)
      para.push(heading ? `<b>${inline(heading[1])}</b>` : inline(line))
    }
  }
  flushPara()
  flushList()
  return out.join('')
}
function onBubbleClick(e: MouseEvent) {
  const a = (e.target as HTMLElement).closest('a[data-internal]') as HTMLAnchorElement | null
  if (!a) return
  e.preventDefault()
  router.push(a.getAttribute('href')!)
}

const shown = (c: AssistantCard) => (c.lines ?? []).slice(0, 6)
const more = (c: AssistantCard) => Math.max(0, (c.lines?.length ?? 0) - 6)
/** A form is only worth showing once: the latest request for it, and only while no bot is connected. */
const lastTelegramCard = computed(() => {
  for (let i = messages.value.length - 1; i >= 0; i--)
    if (messages.value[i].cards.some(c => c.kind === 'telegram')) return messages.value[i].id
  return null
})
</script>

<template>
  <div class="as">
    <header class="as-top">
      <span class="mark">
        <svg viewBox="0 0 40 40" width="32" height="32" aria-hidden="true">
          <rect width="40" height="40" rx="12" fill="#1f7aec" />
          <path d="M14 29V12.5h7.2a6.2 6.2 0 0 1 0 12.4H14" fill="none" stroke="#fff" stroke-width="3.4" stroke-linecap="round" stroke-linejoin="round" />
          <circle cx="28.5" cy="28.5" r="3.2" fill="#35d07f" />
        </svg>
        <span class="mark-text">
          <span>Plum <b>Market</b></span>
          <small>{{ storeName }}</small>
        </span>
      </span>
      <button v-if="progress.length" class="pill" :aria-expanded="showSteps" @click="showSteps = !showSteps">
        <span class="pill-ring" :style="{ '--p': percent }" />{{ doneCount }}/{{ required.length }}
      </button>
      <a class="top-link" :href="shopHref" target="_blank" rel="noopener">Открыть магазин ↗</a>
      <button class="top-go" @click="toPanel">
        <span class="long">{{ onboarded ? 'В админ-панель' : 'Пропустить — в панель' }}</span>
        <span class="short">В панель</span>
      </button>
    </header>

    <div class="as-body">
      <section class="chat">
        <div ref="feedEl" class="feed">
          <div class="feed-inner">
            <p v-if="loading" class="state">Загружаем разговор…</p>
            <div v-else-if="loadError" class="state">
              <p>{{ loadError }}</p>
              <button class="soft" @click="load">Повторить</button>
            </div>

            <div v-if="!loading && !enabled" class="notice">
              ИИ-помощник сейчас недоступен: на сервере не настроен ключ OpenAI. Магазин можно настроить вручную в админ-панели.
            </div>

            <article v-for="m in messages" :key="m.id" class="msg" :class="m.role">
              <span v-if="m.role === 'assistant'" class="avatar"><Icon name="sparkles" /></span>
              <div class="stack">
                <div v-if="m.attachments.length" class="imgs">
                  <img v-for="url in m.attachments" :key="url" :src="url" alt="" loading="lazy" />
                </div>
                <!-- eslint-disable-next-line vue/no-v-html -- render() escapes everything before adding its own tags -->
                <div v-if="m.text && m.role === 'assistant'" class="bubble" @click="onBubbleClick" v-html="render(m.text)" />
                <div v-else-if="m.text" class="bubble">{{ m.text }}</div>

                <template v-for="(c, i) in m.cards" :key="i">
                  <div v-if="c.kind === 'change'" class="card change">
                    <div class="card-title"><span class="ok"><Icon name="check" /></span>{{ c.title }}</div>
                    <ul v-if="shown(c).length">
                      <li v-for="(l, j) in shown(c)" :key="j">{{ l }}</li>
                      <li v-if="more(c)" class="faint">и ещё {{ more(c) }}</li>
                    </ul>
                    <RouterLink v-if="c.link" :to="c.link" class="card-link">{{ c.linkLabel ?? 'Открыть' }} →</RouterLink>
                  </div>
                  <div v-else-if="c.kind === 'availability' && c.productIds?.length" class="card change">
                    <AvailabilityCard :title="c.title" :product-ids="c.productIds" :link="c.link" :link-label="c.linkLabel" />
                  </div>
                  <div v-else-if="c.kind === 'categories' && c.categoryIds?.length" class="card change">
                    <CategoriesCard :title="c.title" :category-ids="c.categoryIds" :link="c.link" :link-label="c.linkLabel" />
                  </div>
                  <RouterLink v-else-if="c.kind === 'link' && c.link" :to="c.link" class="card link">
                    <span>{{ c.title }}</span>
                    <small>{{ c.linkLabel }}</small>
                    <Icon name="chevronRight" />
                  </RouterLink>
                  <div v-else-if="c.kind === 'telegram' && (m.id === lastTelegramCard || telegramDone)" class="card">
                    <TelegramConnectCard :connected="telegramDone ? (botUsername ?? 'подключён') : null" @connected="botConnected" />
                  </div>
                  <div v-else-if="c.kind === 'finish'" class="card finish">
                    <div class="card-title"><span class="ok"><Icon name="check" /></span>{{ c.title }}</div>
                    <p>Все изменения уже на сайте. В админ-панели — заказы, товары и настройки; помощник доступен там в меню слева.</p>
                    <div class="finish-row">
                      <button class="primary" @click="toPanel">Перейти в админ-панель</button>
                      <a class="soft" :href="shopHref" target="_blank" rel="noopener">Открыть магазин ↗</a>
                    </div>
                  </div>
                </template>
              </div>
            </article>

            <article v-if="busy" class="msg assistant">
              <span class="avatar"><Icon name="sparkles" /></span>
              <div class="bubble typing">
                <span class="dots"><i /><i /><i /></span>
                <span>{{ status }}</span>
              </div>
            </article>
          </div>
        </div>

        <div class="dock">
          <div v-if="suggestions.length" class="chips">
            <button v-for="s in suggestions" :key="s" class="chip" @click="send(s)">{{ s }}</button>
          </div>
          <p v-if="sendError" class="send-error">{{ sendError }}</p>
          <form class="composer" :class="{ disabled: !enabled }" @submit.prevent="send()">
            <div v-if="attachments.length || uploading" class="previews">
              <span v-for="(url, i) in attachments" :key="url" class="preview">
                <img :src="url" alt="" />
                <button type="button" aria-label="Убрать фото" @click="attachments.splice(i, 1)"><Icon name="close" /></button>
              </span>
              <span v-if="uploading" class="preview loading">…</span>
            </div>
            <div class="row">
              <button type="button" class="icon-btn" title="Прикрепить фото (до 8): товары, категории, меню или прайс-лист" :disabled="!enabled || attachments.length >= 8"
                @click="fileEl?.click()">
                <Icon name="image" />
              </button>
              <input ref="fileEl" type="file" accept="image/*" multiple hidden @change="attach" />
              <textarea ref="inputEl" v-model="text" rows="1" :disabled="!enabled"
                placeholder="Напишите сообщение…" @input="autosize" @keydown="onKey" />
              <button class="send" :disabled="busy || uploading || !enabled || (!text.trim() && !attachments.length)" aria-label="Отправить">
                <Icon name="send" />
              </button>
            </div>
          </form>
          <p class="fine">Помощник сам вносит изменения в магазин — всё можно проверить и поправить в админ-панели.</p>
        </div>
      </section>

      <div v-if="showSteps" class="scrim" @click="showSteps = false" />
      <aside class="steps" :class="{ open: showSteps }">
        <div class="panel">
          <div class="panel-head">
            <h2>Настройка магазина</h2>
            <button class="close" aria-label="Закрыть" @click="showSteps = false"><Icon name="close" /></button>
          </div>
          <div class="meter"><span :style="{ width: `${percent}%` }" /></div>
          <p class="meter-text">{{ doneCount }} из {{ required.length }} шагов{{ doneCount === required.length ? ' — можно принимать заказы' : '' }}</p>
          <ol>
            <li v-for="s in progress" :key="s.key" :class="{ done: s.done }">
              <span class="tick"><Icon v-if="s.done" name="check" /></span>
              <div class="step-text">
                <RouterLink v-if="s.done" :to="s.link">{{ s.title }}</RouterLink>
                <button v-else :disabled="busy || !enabled" @click="askAbout(s)">{{ s.title }}</button>
                <small>{{ s.hint }}</small>
              </div>
              <em v-if="s.optional && !s.done">по желанию</em>
            </li>
          </ol>
        </div>

        <div class="panel tips">
          <b>Можно просто спросить</b>
          <button v-for="q in ['Сколько брать за доставку?', 'Как сделать хорошие фото товаров?', 'Как получить первые заказы?']" :key="q"
            :disabled="busy || !enabled" @click="showSteps = false; send(q)">{{ q }}</button>
        </div>

        <button class="reset" :disabled="busy" @click="reset">Начать разговор заново</button>
      </aside>
    </div>
  </div>
</template>

<style scoped>
/* Same palette as sign-in and registration: this is the next screen after them. */
.as {
  --blue: #1f7aec;
  --blue-600: #1766cf;
  --blue-50: #eef5ff;
  --blue-100: #dbe9fd;
  --green: #35d07f;
  --ink: #152033;
  --ink-2: #4b5567;
  --ink-3: #8a93a3;
  --line: #e2e8f1;

  height: 100vh;
  height: 100dvh;
  display: flex;
  flex-direction: column;
  color: var(--ink);
  font-size: 15px;
  background:
    radial-gradient(900px 380px at 30% -140px, #dbe9fd 0%, rgb(219 233 253 / 0%) 70%),
    linear-gradient(180deg, #f2f6fc 0%, #f7f9fc 100%);
}
.as a { text-decoration: none; }

/* ---- top bar ---- */
.as-top { flex: none; display: flex; align-items: center; gap: 10px; padding: 12px 20px; border-bottom: 1px solid var(--line); background: rgb(255 255 255 / 70%); backdrop-filter: blur(8px); }
.mark { display: inline-flex; align-items: center; gap: 10px; margin-right: auto; min-width: 0; }
.mark svg { flex: none; }
.mark-text { display: flex; flex-direction: column; min-width: 0; line-height: 1.2; }
.mark-text span { font-size: 15.5px; font-weight: 600; }
.mark-text b { font-weight: 800; color: var(--blue); }
.mark-text small { font-size: 12.5px; color: var(--ink-3); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.top-link { height: 36px; display: inline-flex; align-items: center; padding: 0 14px; border-radius: 999px; color: var(--ink-2); font-weight: 600; font-size: 14px; white-space: nowrap; }
.top-link:hover { color: var(--blue); background: var(--blue-50); }
.top-go { height: 36px; padding: 0 16px; border-radius: 999px; border: 1px solid var(--line); background: #fff; color: var(--ink); font: inherit; font-size: 14px; font-weight: 650; cursor: pointer; white-space: nowrap; }
.top-go:hover { border-color: var(--blue-100); color: var(--blue); }
.top-go .short { display: none; }
.pill { display: none; align-items: center; gap: 6px; height: 34px; padding: 0 12px 0 8px; border-radius: 999px; border: 1px solid var(--line); background: #fff; font: inherit; font-size: 13.5px; font-weight: 650; color: var(--ink); cursor: pointer; }
.pill-ring { width: 18px; height: 18px; border-radius: 50%; background: conic-gradient(var(--green) calc(var(--p) * 1%), #e6ebf3 0); -webkit-mask: radial-gradient(circle 5px, transparent 98%, #000 100%); mask: radial-gradient(circle 5px, transparent 98%, #000 100%); }

/* ---- layout ---- */
.as-body { flex: 1; min-height: 0; display: grid; grid-template-columns: minmax(0, 1fr) 330px; }
.chat { min-height: 0; display: flex; flex-direction: column; }
.feed { flex: 1; min-height: 0; overflow-y: auto; scroll-behavior: auto; }
.feed-inner { max-width: 780px; margin: 0 auto; padding: 28px 20px 12px; display: flex; flex-direction: column; gap: 18px; }
.state { text-align: center; color: var(--ink-3); padding: 40px 0; }
.notice { padding: 12px 14px; border-radius: 12px; background: #fff7e6; color: #8a5a00; font-size: 14px; }

/* ---- messages ---- */
.msg { display: flex; gap: 10px; align-items: flex-start; }
.msg.user { justify-content: flex-end; }
.avatar { flex: none; width: 32px; height: 32px; border-radius: 10px; display: grid; place-items: center; color: #fff; background: linear-gradient(135deg, #1f7aec, #35d07f); box-shadow: 0 4px 12px rgb(31 122 236 / 25%); }
.avatar svg { width: 17px; height: 17px; }
.stack { display: flex; flex-direction: column; gap: 8px; min-width: 0; max-width: min(620px, 100%); }
.msg.user .stack { align-items: flex-end; max-width: min(560px, 88%); }
.bubble { padding: 11px 15px; border-radius: 16px; line-height: 1.55; font-size: 15px; overflow-wrap: anywhere; }
.msg.assistant .bubble { background: #fff; border: 1px solid var(--line); border-top-left-radius: 6px; box-shadow: 0 1px 2px rgb(21 32 51 / 4%); }
.msg.user .bubble { background: var(--blue); color: #fff; border-top-right-radius: 6px; white-space: pre-wrap; }
.bubble :deep(p) { margin: 0; }
.bubble :deep(p + p), .bubble :deep(p + ul), .bubble :deep(p + ol), .bubble :deep(ul + p), .bubble :deep(ol + p) { margin-top: 8px; }
.bubble :deep(ul), .bubble :deep(ol) { margin: 0; padding-left: 20px; display: grid; gap: 3px; }
.bubble :deep(b) { font-weight: 650; }
.bubble :deep(a) { color: var(--blue); font-weight: 550; }
.bubble :deep(a:hover) { text-decoration: underline; }
.imgs { display: flex; gap: 6px; flex-wrap: wrap; justify-content: flex-end; }
.imgs img { width: 120px; height: 120px; object-fit: cover; border-radius: 12px; border: 1px solid var(--line); background: #fff; }

.typing { display: inline-flex; align-items: center; gap: 10px; color: var(--ink-3); font-size: 14px; }
.dots { display: inline-flex; gap: 4px; }
.dots i { width: 6px; height: 6px; border-radius: 50%; background: var(--blue); opacity: .35; animation: blink 1.2s infinite; }
.dots i:nth-child(2) { animation-delay: .2s; }
.dots i:nth-child(3) { animation-delay: .4s; }
@keyframes blink { 0%, 80%, 100% { opacity: .25; transform: translateY(0); } 40% { opacity: 1; transform: translateY(-2px); } }

/* ---- cards under an answer ---- */
.card { background: #fff; border: 1px solid var(--line); border-radius: 14px; padding: 12px 14px; font-size: 14px; }
.card-title { display: flex; align-items: center; gap: 8px; font-weight: 650; }
.ok { width: 20px; height: 20px; flex: none; border-radius: 50%; display: grid; place-items: center; background: #e3f8ec; color: #139a55; }
.ok svg { width: 13px; height: 13px; }
.change { border-left: 3px solid var(--green); }
.change ul { margin: 8px 0 0; padding-left: 28px; display: grid; gap: 2px; color: var(--ink-2); font-size: 13.5px; }
.faint { color: var(--ink-3); }
.card-link { display: inline-block; margin-top: 8px; margin-left: 28px; font-size: 13.5px; font-weight: 600; color: var(--blue); }
.card-link:hover { text-decoration: underline; }
.link { display: flex; align-items: center; gap: 8px; color: var(--ink); font-weight: 600; }
.link small { color: var(--ink-3); font-weight: 500; margin-left: auto; }
.link svg { width: 16px; height: 16px; color: var(--ink-3); }
.link:hover { border-color: var(--blue-100); background: var(--blue-50); }
.finish { border: 1px solid #bfe9d2; background: linear-gradient(180deg, #f1fbf5, #fff); }
.finish p { margin: 8px 0 12px 28px; color: var(--ink-2); font-size: 13.5px; }
.finish-row { display: flex; gap: 8px; flex-wrap: wrap; margin-left: 28px; }
.primary, .soft { height: 38px; display: inline-flex; align-items: center; padding: 0 16px; border-radius: 10px; font: inherit; font-size: 14px; font-weight: 650; cursor: pointer; border: 0; }
.primary { background: var(--blue); color: #fff; }
.primary:hover { background: var(--blue-600); }
.soft { background: var(--blue-50); color: var(--blue); }
.soft:hover { background: var(--blue-100); }

/* ---- input dock ---- */
.dock { flex: none; width: 100%; max-width: 780px; margin: 0 auto; padding: 6px 20px 14px; }
.chips { display: flex; gap: 8px; flex-wrap: wrap; margin: 0 0 10px 42px; }
.chip { height: 34px; padding: 0 14px; border-radius: 999px; border: 1px solid var(--blue-100); background: #fff; color: var(--blue); font: inherit; font-size: 13.5px; font-weight: 600; cursor: pointer; transition: background .15s; }
.chip:hover { background: var(--blue-50); }
.send-error { margin: 0 0 8px; padding: 8px 12px; border-radius: 10px; background: #fdecec; color: #b42318; font-size: 13.5px; }
.composer { background: #fff; border: 1.5px solid var(--line); border-radius: 18px; padding: 6px; box-shadow: 0 1px 2px rgb(21 32 51 / 4%), 0 10px 30px rgb(21 32 51 / 7%); transition: border-color .15s, box-shadow .15s; }
.composer:focus-within { border-color: var(--blue); box-shadow: 0 0 0 4px var(--blue-50); }
.composer.disabled { opacity: .6; }
.row { display: flex; align-items: flex-end; gap: 4px; }
textarea { flex: 1; min-width: 0; border: 0; outline: none; resize: none; font: inherit; font-size: 15px; line-height: 1.45; padding: 9px 6px; max-height: 168px; background: transparent; color: var(--ink); }
textarea::placeholder { color: #a3abb9; }
.icon-btn, .send { flex: none; width: 40px; height: 40px; border-radius: 12px; border: 0; display: grid; place-items: center; cursor: pointer; }
.icon-btn { background: transparent; color: var(--ink-3); }
.icon-btn:hover:not(:disabled) { background: var(--blue-50); color: var(--blue); }
.send { background: var(--blue); color: #fff; }
.send:hover:not(:disabled) { background: var(--blue-600); }
.send:disabled, .icon-btn:disabled { opacity: .45; cursor: default; }
.icon-btn svg, .send svg { width: 19px; height: 19px; }
.previews { display: flex; gap: 6px; padding: 4px 4px 6px; flex-wrap: wrap; }
.preview { position: relative; width: 58px; height: 58px; border-radius: 10px; overflow: hidden; border: 1px solid var(--line); display: grid; place-items: center; color: var(--ink-3); }
.preview img { width: 100%; height: 100%; object-fit: cover; }
.preview button { position: absolute; top: 3px; right: 3px; width: 20px; height: 20px; border-radius: 50%; border: 0; background: rgb(21 32 51 / 70%); color: #fff; display: grid; place-items: center; cursor: pointer; padding: 0; }
.preview button svg { width: 12px; height: 12px; }
.fine { margin: 8px 0 0; text-align: center; font-size: 12px; color: var(--ink-3); }

/* ---- progress ---- */
.steps { min-height: 0; overflow-y: auto; padding: 24px 20px 20px 0; display: flex; flex-direction: column; gap: 12px; }
.panel { background: #fff; border: 1px solid var(--line); border-radius: 18px; padding: 16px 16px 14px; }
.panel-head { display: flex; align-items: center; }
.panel-head h2 { font-size: 15.5px; font-weight: 700; margin-right: auto; }
.close { display: none; width: 32px; height: 32px; border: 0; border-radius: 9px; background: none; color: var(--ink-3); cursor: pointer; place-items: center; }
.close svg { width: 18px; height: 18px; }
.meter { height: 8px; margin-top: 12px; border-radius: 999px; background: #edf1f7; overflow: hidden; }
.meter span { display: block; height: 100%; border-radius: inherit; background: linear-gradient(90deg, var(--blue), var(--green)); transition: width .5s ease; }
.meter-text { margin: 8px 0 10px; font-size: 13px; color: var(--ink-3); }
.steps ol { list-style: none; margin: 0; padding: 0; display: grid; gap: 2px; }
.steps li { display: flex; align-items: flex-start; gap: 10px; padding: 8px 6px; border-radius: 10px; }
.tick { width: 20px; height: 20px; flex: none; margin-top: 1px; border-radius: 50%; border: 1.5px solid #cdd5e1; display: grid; place-items: center; color: #fff; transition: background .2s, border-color .2s; }
.tick svg { width: 12px; height: 12px; }
li.done .tick { background: var(--green); border-color: var(--green); }
.step-text { display: flex; flex-direction: column; min-width: 0; }
.step-text a, .step-text button { font: inherit; font-size: 14px; font-weight: 600; text-align: left; padding: 0; border: 0; background: none; cursor: pointer; }
.step-text button { color: var(--ink); }
.step-text button:hover:not(:disabled) { color: var(--blue); }
.step-text button:disabled { cursor: default; }
.step-text a { color: var(--ink-3); text-decoration: line-through; text-decoration-color: #c5ccd8; }
.step-text a:hover { color: var(--blue); }
.step-text small { font-size: 12.5px; color: var(--ink-3); }
.steps em { margin-left: auto; font-style: normal; font-size: 11px; color: var(--ink-3); white-space: nowrap; padding-top: 2px; }
.tips { display: flex; flex-direction: column; gap: 6px; }
.tips b { font-size: 13.5px; margin-bottom: 2px; }
.tips button { text-align: left; font: inherit; font-size: 13.5px; color: var(--blue); background: var(--blue-50); border: 0; border-radius: 10px; padding: 8px 11px; cursor: pointer; }
.tips button:hover:not(:disabled) { background: var(--blue-100); }
.tips button:disabled { opacity: .6; cursor: default; }
.reset { align-self: center; border: 0; background: none; color: var(--ink-3); font: inherit; font-size: 12.5px; cursor: pointer; padding: 4px 8px; }
.reset:hover:not(:disabled) { color: var(--blue); }
.scrim { display: none; }

@media (max-width: 1000px) {
  .as-body { grid-template-columns: minmax(0, 1fr); }
  .pill { display: inline-flex; }
  .steps {
    position: fixed; top: 0; right: 0; bottom: 0; width: min(360px, 92vw); z-index: 40; padding: 16px; background: #f4f7fb;
    transform: translateX(100%); transition: transform .2s ease; box-shadow: -12px 0 40px rgb(21 32 51 / 18%);
  }
  .steps.open { transform: none; }
  .close { display: grid; }
  .scrim { display: block; position: fixed; inset: 0; background: rgb(21 32 51 / 30%); z-index: 35; }
}
@media (max-width: 640px) {
  .as-top { padding: 10px 14px; gap: 6px; }
  .top-link { display: none; }
  .top-go { padding: 0 12px; font-size: 13px; }
  .top-go .long { display: none; }
  .top-go .short { display: inline; }
  .mark-text span { display: none; }
  .mark-text small { max-width: 150px; font-size: 14.5px; font-weight: 650; color: var(--ink); }
  .feed-inner { padding: 18px 14px 8px; gap: 14px; }
  .dock { padding: 4px 12px 10px; }
  .chips { margin-left: 0; }
  .avatar { width: 28px; height: 28px; }
  .imgs img { width: 96px; height: 96px; }
  .fine { display: none; }
}
</style>
