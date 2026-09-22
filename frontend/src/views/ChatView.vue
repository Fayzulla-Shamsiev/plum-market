<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { catalogApi, chatApi, type ChatChannel, type ConversationDetail, type ConversationRow, type Upload } from '../api'
import Icon from '../components/Icon.vue'
import PlatformIcon from '../components/PlatformIcon.vue'
import { channelLabel, count, loc, timeShort } from '../format'
import { refreshChatUnread } from '../store'
import ChatSettingsPanel from './chat/ChatSettingsPanel.vue'

const route = useRoute()
const router = useRouter()

// Categories from the spec: Все, Не прочитано, Instagram, Обзоры.
const categories = [
  { key: 'all', label: 'Все' },
  { key: 'unread', label: 'Не прочитано' },
  { key: 'instagram', label: 'Instagram' },
  { key: 'reviews', label: 'Обзоры' },
]
const channels: ChatChannel[] = ['Telegram', 'Instagram', 'Website', 'Wolt']

const filter = ref('all')
const channel = ref<ChatChannel | ''>('')
const search = ref('')
const list = ref<ConversationRow[]>([])
const counts = ref({ unread: 0, reviews: 0 })
const listLoaded = ref(false)

const activeId = computed(() => (route.params.id ? Number(route.params.id) : null))
const conv = ref<ConversationDetail | null>(null)
const convError = ref('')

const text = ref('')
const pending = ref<Upload | null>(null)
const uploading = ref(false)
const sending = ref(false)
const emojiOpen = ref(false)
const settingsOpen = ref(false)
const messagesEl = ref<HTMLElement>()
const fileInput = ref<HTMLInputElement>()
const inputEl = ref<HTMLTextAreaElement>()

const emojis = ['😊', '🙂', '😉', '😍', '🥰', '😂', '🙏', '👍', '👌', '👏', '🔥', '❤️', '🎉', '✅', '⏰', '🚚', '📦', '🛵',
  '🥐', '🍕', '🍔', '☕', '🍰', '🎂', '🍩', '🥗', '😔', '😅', '🤔', '👋', '💳', '📍']

async function loadList() {
  try {
    const r = await chatApi.conversations({ filter: filter.value, channel: channel.value, search: search.value.trim() })
    list.value = r.items
    counts.value = r.counts
  } catch {
    // Server unreachable: keep showing the last list; the next poll retries.
  } finally {
    listLoaded.value = true
  }
}

async function loadConversation(scroll = true) {
  if (!activeId.value) { conv.value = null; return }
  convError.value = ''
  try {
    const was = conv.value?.messages.length ?? 0
    const detail = await chatApi.conversation(activeId.value)
    const changed = !conv.value || conv.value.id !== detail.id || detail.messages.length !== was
    conv.value = detail
    // Opening a thread marks it read on the server; reflect that in the list immediately.
    const row = list.value.find(r => r.id === detail.id)
    if (row && row.unreadCount) { row.unreadCount = 0; refreshChatUnread() }
    if (changed && scroll) scrollToBottom()
  } catch (e) {
    convError.value = (e as Error).message
  }
}

function scrollToBottom() {
  nextTick(() => { if (messagesEl.value) messagesEl.value.scrollTop = messagesEl.value.scrollHeight })
}

let searchTimer = 0
watch(search, () => { clearTimeout(searchTimer); searchTimer = window.setTimeout(loadList, 250) })
watch([filter, channel], loadList)
watch(activeId, () => { text.value = ''; pending.value = null; loadConversation() })

// Clients page → "Чат": /chat?customer=<id> resolves to (or creates) that customer's thread.
async function openFromQuery() {
  const customer = route.query.customer
  if (!customer) return
  const { id } = await chatApi.forCustomer(Number(customer))
  await router.replace({ path: `/chat/${id}` })
  loadList()
}

// Channel webhooks would push messages in real time; the prototype polls instead.
let poll = 0
onMounted(async () => {
  await openFromQuery()
  loadList()
  loadConversation()
  poll = window.setInterval(() => { loadList(); if (activeId.value) loadConversation() }, 6000)
})
onBeforeUnmount(() => clearInterval(poll))

function open(id: number) {
  router.push(`/chat/${id}`)
}

async function pickFile(ev: Event) {
  const file = (ev.target as HTMLInputElement).files?.[0]
  ;(ev.target as HTMLInputElement).value = ''
  if (!file) return
  uploading.value = true
  try {
    pending.value = await catalogApi.upload(file)
  } catch (e) {
    convError.value = (e as Error).message
  } finally {
    uploading.value = false
  }
}

function addEmoji(e: string) {
  const el = inputEl.value
  const pos = el?.selectionStart ?? text.value.length
  text.value = text.value.slice(0, pos) + e + text.value.slice(pos)
  emojiOpen.value = false
  nextTick(() => { el?.focus(); el?.setSelectionRange(pos + e.length, pos + e.length) })
}

async function sendMessage() {
  if (!conv.value || sending.value || (!text.value.trim() && !pending.value)) return
  sending.value = true
  try {
    conv.value = await chatApi.send(conv.value.id, {
      text: text.value.trim(),
      attachmentUrl: pending.value?.url,
      attachmentName: pending.value?.name,
      attachmentType: pending.value?.type,
      senderName: 'Азиз Каримов',
    })
    text.value = ''
    pending.value = null
    scrollToBottom()
    loadList()
  } catch (e) {
    convError.value = (e as Error).message
  } finally {
    sending.value = false
  }
}

function onKey(e: KeyboardEvent) {
  // Enter sends; Shift+Enter makes a new line.
  if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) {
    e.preventDefault()
    sendMessage()
  }
}

async function simulate() {
  const r = await chatApi.simulate()
  await loadList()
  refreshChatUnread()
  if (r.id === activeId.value) loadConversation()
}

/** Messages grouped under a date divider. */
const grouped = computed(() => {
  const groups: { day: string; items: ConversationDetail['messages'] }[] = []
  for (const m of conv.value?.messages ?? []) {
    const day = new Date(m.sentAt).toLocaleDateString('ru-RU', { day: 'numeric', month: 'long', year: 'numeric' })
    const last = groups[groups.length - 1]
    if (last?.day === day) last.items.push(m)
    else groups.push({ day, items: [m] })
  }
  return groups
})

const initials = (name: string) => name.split(/\s+/).map(p => p[0]).filter(Boolean).slice(0, 2).join('').toUpperCase()
const time = (iso: string) => new Date(iso).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' })
const stars = (n: number) => '★'.repeat(n) + '☆'.repeat(5 - n)
</script>

<template>
  <div class="chat-page">
    <aside class="inbox" :class="{ hideOnMobile: activeId }">
      <div class="inbox-head">
        <h1>Все чаты</h1>
        <select v-model="channel" class="select sm" aria-label="Платформа">
          <option value="">Все платформы</option>
          <option v-for="c in channels" :key="c" :value="c">{{ channelLabel[c] }}</option>
        </select>
        <button class="btn btn-ghost btn-icon" title="Настройки чата" aria-label="Настройки чата" @click="settingsOpen = true">
          <Icon name="settings" />
        </button>
      </div>
      <label class="search inbox-search">
        <Icon name="search" />
        <input v-model="search" class="input" placeholder="Поиск по чатам" aria-label="Поиск по чатам" />
      </label>
      <div class="cats" role="tablist">
        <button v-for="c in categories" :key="c.key" role="tab" :aria-selected="filter === c.key"
                class="cat" :class="{ active: filter === c.key }" @click="filter = c.key">
          {{ c.label }}
          <span v-if="c.key === 'unread' && counts.unread" class="n">{{ counts.unread }}</span>
          <span v-if="c.key === 'reviews' && counts.reviews" class="n">{{ counts.reviews }}</span>
        </button>
      </div>

      <ul class="threads">
        <li v-for="c in list" :key="c.id">
          <button class="thread" :class="{ active: c.id === activeId, unread: c.unreadCount > 0 }" @click="open(c.id)">
            <span class="avatar" :class="c.channel">
              {{ c.channel === 'Wolt' ? 'W' : initials(c.displayName) }}
              <PlatformIcon :platform="c.channel" class="mini" />
            </span>
            <span class="t-body">
              <span class="t-top">
                <b class="t-name">{{ c.displayName }}</b>
                <span class="t-time faint">{{ timeShort(c.lastMessageAt) }}</span>
              </span>
              <span class="t-bottom">
                <span class="t-last">
                  <Icon v-if="c.reviewId" name="star" class="review-ic" />{{ c.lastMessageText }}
                </span>
                <span v-if="c.unreadCount" class="unread-dot">{{ c.unreadCount }}</span>
              </span>
            </span>
          </button>
        </li>
        <li v-if="listLoaded && !list.length" class="empty">Диалогов не найдено</li>
        <li v-if="!listLoaded"><div class="skeleton" style="height: 320px; margin: 10px" /></li>
      </ul>
      <button class="btn btn-ghost sim" title="Имитировать входящее сообщение от клиента" @click="simulate">
        <Icon name="zap" />Входящее сообщение (тест)
      </button>
    </aside>

    <section class="thread-pane" :class="{ hideOnMobile: !activeId }">
      <div v-if="!activeId" class="placeholder">
        <Icon name="chat" />
        <p>Выберите диалог слева</p>
        <p class="faint small">Сообщения из Telegram, Instagram, сайта и Wolt — в одном окне</p>
      </div>

      <template v-else-if="conv">
        <header class="conv-head">
          <button class="btn btn-ghost btn-icon back" aria-label="Назад к списку" @click="router.push('/chat')"><Icon name="chevronLeft" /></button>
          <span class="avatar lg" :class="conv.channel">{{ conv.channel === 'Wolt' ? 'W' : initials(conv.displayName) }}</span>
          <div class="grow who">
            <div class="row" style="gap: 8px">
              <b>{{ conv.displayName }}</b>
              <PlatformIcon :platform="conv.channel" show-label class="muted small" />
            </div>
            <div class="faint small">
              <template v-if="conv.customer">
                <span class="num">{{ conv.customer.phone }}</span>
                <span v-if="conv.handle"> · {{ conv.handle }}</span>
                · заказов: {{ conv.customer.orders }} · бонусы: {{ count(conv.customer.bonusPoints) }}
              </template>
              <template v-else>{{ conv.handle ?? 'Сообщения из агрегатора' }}</template>
            </div>
          </div>
          <RouterLink v-if="conv.customer" class="btn btn-sm" :to="{ path: '/customers', query: { open: conv.customer.id } }">
            Профиль клиента
          </RouterLink>
        </header>

        <div v-if="conv.review" class="review-bar">
          <span class="stars">{{ stars(conv.review.rating) }}</span>
          Отзыв о товаре <b>«{{ loc(conv.review.productName) }}»</b>
          <span class="badge" :class="conv.review.status === 'Answered' ? 'Completed' : 'New'">
            {{ conv.review.status === 'Answered' ? 'Отвечено' : 'Новый' }}
          </span>
          <span class="faint small">Ваш ответ будет опубликован под отзывом</span>
        </div>

        <div ref="messagesEl" class="messages">
          <template v-for="g in grouped" :key="g.day">
            <div class="day"><span>{{ g.day }}</span></div>
            <div v-for="m in g.items" :key="m.id" class="msg" :class="[m.direction === 'Out' ? 'out' : 'in', { auto: m.isAuto }]">
              <div class="bubble">
                <a v-if="m.attachmentUrl && m.attachmentType === 'image'" :href="m.attachmentUrl" target="_blank" rel="noopener">
                  <img :src="m.attachmentUrl" :alt="m.attachmentName ?? 'Изображение'" class="att-img" />
                </a>
                <video v-else-if="m.attachmentUrl && m.attachmentType === 'video'" :src="m.attachmentUrl" controls class="att-img" />
                <a v-else-if="m.attachmentUrl" :href="m.attachmentUrl" target="_blank" rel="noopener" class="att-file">
                  <Icon name="file" />{{ m.attachmentName }}
                </a>
                <div v-if="m.text" class="text">{{ m.text }}</div>
                <div class="meta">
                  <span v-if="m.isAuto" class="auto-tag"><Icon name="bot" />Автоответ</span>
                  <span v-else-if="m.direction === 'Out' && m.senderName">{{ m.senderName }} ·</span>
                  {{ time(m.sentAt) }}
                </div>
              </div>
            </div>
          </template>
          <div v-if="!conv.messages.length" class="empty">Сообщений пока нет — напишите первым</div>
        </div>

        <div v-if="convError" class="error-banner composer-error">{{ convError }}</div>

        <footer class="composer">
          <div v-if="pending" class="pending">
            <img v-if="pending.type === 'image'" :src="pending.url" alt="" />
            <Icon v-else :name="pending.type === 'video' ? 'video' : 'file'" />
            <span class="grow ellipsis">{{ pending.name }}</span>
            <button class="btn btn-ghost btn-icon btn-sm" aria-label="Убрать вложение" @click="pending = null"><Icon name="close" /></button>
          </div>
          <div class="compose-row">
            <input ref="fileInput" type="file" hidden @change="pickFile" />
            <button class="btn btn-ghost btn-icon" :disabled="uploading" title="Прикрепить файл" aria-label="Прикрепить файл" @click="fileInput?.click()">
              <Icon name="paperclip" />
            </button>
            <div class="emoji-wrap">
              <button class="btn btn-ghost btn-icon" title="Эмодзи" aria-label="Эмодзи" :aria-expanded="emojiOpen" @click="emojiOpen = !emojiOpen">
                <Icon name="smile" />
              </button>
              <div v-if="emojiOpen" class="emoji-pop" @mouseleave="emojiOpen = false">
                <button v-for="e in emojis" :key="e" type="button" @click="addEmoji(e)">{{ e }}</button>
              </div>
            </div>
            <textarea ref="inputEl" v-model="text" class="textarea grow" rows="1"
                      :placeholder="conv.review ? 'Ответ на отзыв…' : 'Написать сообщение…'" aria-label="Сообщение" @keydown="onKey" />
            <button class="btn btn-primary btn-icon send" :disabled="sending || uploading || (!text.trim() && !pending)" aria-label="Отправить" @click="sendMessage">
              <Icon name="send" />
            </button>
          </div>
          <div class="hint faint">Enter — отправить · Shift+Enter — новая строка</div>
        </footer>
      </template>
      <div v-else-if="convError" class="placeholder"><p>{{ convError }}</p></div>
      <div v-else class="placeholder"><div class="skeleton" style="height: 200px; width: 60%" /></div>
    </section>

    <ChatSettingsPanel v-if="settingsOpen" @close="settingsOpen = false" />
  </div>
</template>

<style scoped>
.chat-page { display: flex; height: 100vh; background: var(--surface); }
.inbox { width: 360px; flex: none; border-right: 1px solid var(--border); display: flex; flex-direction: column; min-height: 0; }
.inbox-head { display: flex; align-items: center; gap: 8px; padding: 18px 14px 10px 18px; }
.inbox-head h1 { font-size: 19px; margin-right: auto; white-space: nowrap; }
.select.sm { height: 30px; font-size: 13px; padding: 0 6px; max-width: 140px; }
.inbox-search { margin: 0 14px 10px; display: block; }
.inbox-search .input { width: 100%; }
.cats { display: flex; flex-wrap: wrap; gap: 4px; padding: 0 14px 10px; border-bottom: 1px solid var(--border); }
.cat { border: 0; background: none; font: inherit; font-size: 13px; font-weight: 550; padding: 6px 8px; border-radius: 8px; color: var(--text-2); cursor: pointer; white-space: nowrap; display: inline-flex; gap: 5px; align-items: center; }
.cat:hover { background: var(--plum-50); }
.cat.active { background: var(--plum-100); color: var(--plum-700); }
.cat .n { background: #e34948; color: #fff; border-radius: 9px; font-size: 11px; padding: 0 6px; line-height: 17px; }
.threads { list-style: none; margin: 0; padding: 6px; overflow-y: auto; flex: 1; }
.thread { display: flex; gap: 10px; width: 100%; padding: 9px 10px; border: 0; background: none; border-radius: 10px; cursor: pointer; text-align: left; font: inherit; color: inherit; }
.thread:hover { background: var(--surface-2); }
.thread.active { background: var(--plum-50); }
.avatar { position: relative; flex: none; width: 42px; height: 42px; border-radius: 50%; display: grid; place-items: center; font-weight: 700; font-size: 14px; background: var(--plum-100); color: var(--plum-700); }
.avatar.Instagram { background: #fde7f0; color: #b1245c; }
.avatar.Website { background: #fdeee6; color: #b24a1e; }
.avatar.Wolt { background: #dff7fc; color: #007a92; }
.avatar.lg { width: 40px; height: 40px; }
.avatar .mini { position: absolute; right: -3px; bottom: -3px; }
.avatar .mini :deep(svg) { width: 17px; height: 17px; border: 2px solid var(--surface); border-radius: 50%; }
.t-body { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 2px; }
.t-top, .t-bottom { display: flex; align-items: center; gap: 8px; }
.t-name { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-weight: 600; }
.t-time { font-size: 12px; flex: none; }
.t-last { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; color: var(--text-2); font-size: 13px; }
.thread.unread .t-name, .thread.unread .t-last { color: var(--text); font-weight: 650; }
.review-ic { width: 13px; height: 13px; color: #eda100; vertical-align: -2px; margin-right: 4px; fill: #eda100; }
.unread-dot { flex: none; min-width: 20px; height: 20px; border-radius: 10px; background: var(--plum-600); color: #fff; font-size: 11px; font-weight: 700; display: grid; place-items: center; padding: 0 5px; }
.sim { margin: 6px 10px 10px; justify-content: center; font-size: 13px; color: var(--text-2); }

.thread-pane { flex: 1; min-width: 0; display: flex; flex-direction: column; background: #f4f1f7; }
.placeholder { flex: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 6px; color: var(--text-2); }
.placeholder > svg { width: 44px; height: 44px; color: var(--plum-500); opacity: .7; }
.placeholder p { margin: 0; }
.conv-head { display: flex; align-items: center; gap: 12px; padding: 12px 18px; background: var(--surface); border-bottom: 1px solid var(--border); }
.who { min-width: 0; }
.back { display: none; }
.review-bar { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; padding: 10px 18px; background: #fff8e6; border-bottom: 1px solid #f3e2b3; font-size: 13px; }
.stars { color: #d99100; letter-spacing: 1px; }
.messages { flex: 1; overflow-y: auto; padding: 16px 22px; display: flex; flex-direction: column; gap: 6px; }
.day { text-align: center; margin: 10px 0 4px; }
.day span { font-size: 12px; background: rgb(255 255 255 / 80%); color: var(--text-2); padding: 3px 10px; border-radius: 10px; }
.msg { display: flex; }
.msg.out { justify-content: flex-end; }
.bubble { max-width: min(560px, 75%); padding: 8px 12px 6px; border-radius: 14px; background: var(--surface); box-shadow: var(--shadow); }
.msg.in .bubble { border-bottom-left-radius: 4px; }
.msg.out .bubble { background: #e6dcf5; border-bottom-right-radius: 4px; }
.msg.auto .bubble { background: #eef3fb; border: 1px dashed #b7cbe8; }
.text { white-space: pre-wrap; word-wrap: break-word; }
.meta { font-size: 11px; color: var(--text-3); text-align: right; margin-top: 2px; display: flex; gap: 4px; justify-content: flex-end; align-items: center; }
.auto-tag { display: inline-flex; align-items: center; gap: 3px; color: var(--info); }
.auto-tag svg { width: 12px; height: 12px; }
.att-img { display: block; max-width: 280px; max-height: 240px; border-radius: 10px; margin-bottom: 4px; }
.att-file { display: inline-flex; align-items: center; gap: 6px; padding: 6px 10px; border-radius: 8px; background: rgb(0 0 0 / 5%); margin-bottom: 4px; }
.att-file svg { width: 16px; height: 16px; }
.composer { background: var(--surface); border-top: 1px solid var(--border); padding: 10px 14px 8px; }
.composer-error { margin: 0 14px 8px; }
.compose-row { display: flex; align-items: flex-end; gap: 6px; }
.compose-row .textarea { resize: none; min-height: 38px; max-height: 140px; field-sizing: content; }
.send { width: 38px; height: 38px; }
.pending { display: flex; align-items: center; gap: 10px; padding: 6px 8px; margin-bottom: 8px; background: var(--surface-2); border: 1px solid var(--border); border-radius: 8px; }
.pending img { width: 40px; height: 40px; object-fit: cover; border-radius: 6px; }
.pending svg { width: 20px; height: 20px; }
.ellipsis { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.emoji-wrap { position: relative; }
.emoji-pop { position: absolute; bottom: 42px; left: 0; width: 272px; display: grid; grid-template-columns: repeat(8, 1fr); gap: 2px; padding: 8px; background: var(--surface); border: 1px solid var(--border); border-radius: 12px; box-shadow: var(--shadow-lg); z-index: 5; }
.emoji-pop button { border: 0; background: none; font-size: 20px; padding: 3px; border-radius: 6px; cursor: pointer; }
.emoji-pop button:hover { background: var(--plum-50); }
.hint { font-size: 11px; margin-top: 4px; padding-left: 4px; }
.small { font-size: 12px; }
.empty { padding: 30px; text-align: center; color: var(--text-3); }

@media (max-width: 960px) {
  .chat-page { height: calc(100vh); padding-top: 56px; }
  .inbox { width: 100%; border-right: 0; }
  .hideOnMobile { display: none; }
  .back { display: inline-flex; }
}
</style>
