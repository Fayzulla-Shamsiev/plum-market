<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { shopApi, type ChatThread, type ShopOrder } from '../api'
import { statusText } from '../orderStatus'
import { signedIn } from '../state/auth'
import { price, t } from '../i18n'
import { chatName, chatOpen, chatOrder, chatProduct, chatToken } from '../state/chat'
import { useMeta } from '../store'
import SIcon from './SIcon.vue'

const meta = useMeta()

// Chat with the store. Messages go to the admin inbox (Чат → «Сайт»); replies are polled while the drawer is open.
const thread = ref<ChatThread | null>(null)
const nameDraft = ref(chatName.value)
const text = ref('')
const sending = ref(false)
const error = ref('')
const list = ref<HTMLElement>()
const input = ref<HTMLTextAreaElement>()
// Before the first message we ask for a name (optional). "Skip" is allowed, so an empty name still counts.
// Signed-in customers are known already; guests are asked once what to call them.
const introDone = ref(!!chatName.value || signedIn.value)
const started = computed(() => introDone.value || !!thread.value?.messages.length)

// The thread is created on the first message, so just opening the drawer doesn't add an empty dialog to the inbox.
async function load() {
  try {
    thread.value = await shopApi.chatGet(chatToken.value)
    scrollDown()
  } catch { thread.value = null }
}
function start() {
  chatName.value = nameDraft.value.trim()
  introDone.value = true
}
async function send() {
  const v = text.value.trim()
  if (!v || sending.value) return
  sending.value = true
  error.value = ''
  try {
    if (!thread.value) await shopApi.chatOpen(chatToken.value, chatName.value || undefined)
    thread.value = await shopApi.chatSend(chatToken.value, v, { productId: chatProduct.value?.id, orderId: chatOrder.value ?? undefined })
    text.value = ''
    chatProduct.value = null // the product / order is attached to the question it was asked with
    chatOrder.value = null
    scrollDown()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    sending.value = false
  }
}
function onKey(e: KeyboardEvent) {
  if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) { e.preventDefault(); send() }
}
async function scrollDown() {
  await nextTick()
  list.value?.scrollTo({ top: list.value.scrollHeight })
}

// Poll for the merchant's replies.
let timer: number | undefined
async function poll() {
  if (!thread.value) return
  try {
    const next = await shopApi.chatGet(chatToken.value)
    const grew = next.messages.length !== thread.value.messages.length
    thread.value = next
    if (grew) scrollDown()
  } catch { /* keep the last state */ }
}
// "Чат с поддержкой": the customer's recent orders, so a question can point at one of them.
const orders = ref<ShopOrder[]>([])
async function loadOrders() {
  orders.value = signedIn.value ? (await shopApi.orders('all').catch(() => null))?.items.slice(0, 8) ?? [] : []
}
watch(signedIn, s => { introDone.value = introDone.value || s; load(); loadOrders() })
const pickOrder = (id: number) => { chatOrder.value = chatOrder.value === id ? null : id; chatProduct.value = null; input.value?.focus() }
const dropOrder = () => { chatOrder.value = null }

onMounted(() => {
  load()
  loadOrders()
  timer = window.setInterval(poll, 5000)
  document.addEventListener('keydown', esc)
})
onBeforeUnmount(() => { clearInterval(timer); document.removeEventListener('keydown', esc) })
const close = () => { chatOpen.value = false }
const dropProduct = () => { chatProduct.value = null }
const esc = (e: KeyboardEvent) => e.key === 'Escape' && close()
watch(started, s => s && nextTick(() => input.value?.focus()))

// Questions about a product or an order start with a reference line: "🛍 Name (/product/ID)" or "📦 Заказ №N (/profile/orders/N)".
function parse(textValue: string) {
  const m = /^(🛍|📦) (.+) \((\/(?:product|profile\/orders)\/\d+)\)\n([\s\S]*)$/.exec(textValue)
  return m ? { ref: { icon: m[1], label: m[2], to: m[3] }, text: m[4] } : { ref: null, text: textValue }
}
const time = (iso: string) => new Date(iso).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' })
</script>

<template>
  <div class="scrim" @mousedown.self="close">
    <aside class="drawer" role="dialog" aria-modal="true" :aria-label="t('chatTitle')">
      <header>
        <span class="ava"><SIcon name="store" :size="20" /></span>
        <div class="who">
          <b>{{ thread?.store ?? meta?.store.name ?? '…' }}</b>
          <small>{{ signedIn ? t('supportTitle') : t('chatTitle') }}</small>
        </div>
        <button class="x" :aria-label="t('close')" @click="close"><SIcon name="close" /></button>
      </header>

      <div ref="list" class="messages">
        <p class="hello">{{ t('chatHello') }}</p>
        <template v-if="thread">
          <div v-for="m in thread.messages" :key="m.id" class="msg" :class="{ mine: m.mine }">
            <div class="bubble">
              <RouterLink v-if="parse(m.text).ref" :to="parse(m.text).ref!.to" class="about" @click="close">
                {{ parse(m.text).ref!.icon }} {{ parse(m.text).ref!.label }}
              </RouterLink>
              <img v-if="m.attachmentUrl && m.attachmentType === 'image'" :src="m.attachmentUrl" alt="" class="att" />
              <span class="txt">{{ parse(m.text).text }}</span>
              <small>{{ m.isAuto ? t('autoReply') + ' · ' : '' }}{{ time(m.sentAt) }}</small>
            </div>
          </div>
        </template>
      </div>

      <p v-if="error" class="err">{{ error }}</p>

      <form v-if="!started" class="intro" @submit.prevent="start">
        <label>
          <span>{{ t('chatName') }}</span>
          <input v-model="nameDraft" maxlength="60" autocomplete="given-name" />
        </label>
        <button class="s-btn" type="submit">{{ t('chatStart') }}</button>
      </form>

      <template v-else>
        <div v-if="orders.length" class="orders">
          <span class="orders-lbl">{{ t('yourOrders') }}</span>
          <div class="s-scroll">
            <button v-for="o in orders" :key="o.id" type="button" class="o-chip" :class="{ on: chatOrder === o.id }" @click="pickOrder(o.id)">
              №{{ o.id }} · {{ statusText(o.status, o.deliveryType) }}
            </button>
          </div>
        </div>
        <div v-if="chatOrder" class="ctx">
          <span class="ctx-lbl">{{ t('aboutOrder') }}</span>
          <RouterLink :to="`/profile/orders/${chatOrder}`" class="ctx-link" @click="close"><b>№{{ chatOrder }}</b></RouterLink>
          <button type="button" :aria-label="t('clear')" @click="dropOrder"><SIcon name="close" :size="14" /></button>
        </div>
        <div v-if="chatProduct" class="ctx">
          <span class="ctx-lbl">{{ t('chatAbout') }}</span>
          <b>{{ chatProduct.name }}</b>
          <span class="faint">{{ price(chatProduct.price) }}</span>
          <button type="button" :aria-label="t('clear')" @click="dropProduct"><SIcon name="close" :size="14" /></button>
        </div>
        <form class="composer" @submit.prevent="send">
          <textarea ref="input" v-model="text" rows="1" maxlength="2000" :placeholder="t('chatPlaceholder')" @keydown="onKey" />
          <button class="send" type="submit" :disabled="!text.trim() || sending" :aria-label="t('send')"><SIcon name="send" :size="18" /></button>
        </form>
      </template>
    </aside>
  </div>
</template>

<style scoped>
.scrim { position: fixed; inset: 0; z-index: 110; background: rgb(21 32 51 / 35%); display: flex; justify-content: flex-end; animation: fade .15s; }
@keyframes fade { from { opacity: 0; } }
.drawer { width: min(420px, 100%); height: 100%; background: var(--card); display: flex; flex-direction: column; box-shadow: var(--lift-lg); animation: slide .2s ease-out; }
@keyframes slide { from { transform: translateX(30px); } }
header { display: flex; align-items: center; gap: 12px; padding: 14px 16px; border-bottom: 1px solid var(--line); }
.ava { width: 42px; height: 42px; border-radius: 50%; background: var(--blue); color: #fff; display: grid; place-items: center; flex: none; }
.who { flex: 1; display: flex; flex-direction: column; line-height: 1.25; }
.who small { color: var(--green); font-size: 12.5px; }
.x { width: 38px; height: 38px; border: 0; border-radius: 11px; background: var(--page); cursor: pointer; display: grid; place-items: center; }
.messages { flex: 1; overflow-y: auto; padding: 16px; display: flex; flex-direction: column; gap: 8px; background: #f7f9fc; }
.hello { align-self: center; text-align: center; color: var(--ink-3); font-size: 13.5px; margin: 4px 16px 10px; }
.msg { display: flex; }
.msg.mine { justify-content: flex-end; }
.bubble { max-width: 82%; background: var(--card); border-radius: 16px 16px 16px 4px; padding: 9px 12px; box-shadow: 0 1px 2px rgb(21 32 51 / 6%); display: flex; flex-direction: column; gap: 3px; }
.mine .bubble { background: var(--blue); color: #fff; border-radius: 16px 16px 4px 16px; }
.txt { white-space: pre-wrap; word-break: break-word; line-height: 1.45; }
.bubble small { align-self: flex-end; font-size: 11px; opacity: .6; }
.about { font-size: 12.5px; font-weight: 600; padding: 5px 8px; border-radius: 8px; background: rgb(255 255 255 / 18%); align-self: flex-start; }
.msg:not(.mine) .about { background: var(--blue-50); color: var(--blue); }
.att { max-width: 220px; border-radius: 10px; }
.err { margin: 0; padding: 8px 16px; color: var(--red); font-size: 13px; }
.intro { padding: 16px; display: flex; flex-direction: column; gap: 12px; border-top: 1px solid var(--line); }
.intro label { display: flex; flex-direction: column; gap: 6px; font-size: 14px; color: var(--ink-2); }
.intro input { height: 44px; border: 1.5px solid var(--line); border-radius: 12px; padding: 0 12px; font: inherit; }
.intro input:focus { border-color: var(--blue); outline: none; }
.ctx { display: flex; align-items: center; gap: 8px; margin: 10px 12px 0; padding: 8px 10px; border-radius: 12px; background: var(--blue-50); font-size: 13px; }
.ctx-lbl { color: var(--blue-600); }
.ctx b { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; min-width: 0; }
.ctx .faint { color: var(--ink-3); white-space: nowrap; }
.ctx button { margin-left: auto; border: 0; background: none; cursor: pointer; color: var(--ink-3); display: grid; place-items: center; }
.orders { padding: 10px 12px 0; display: flex; flex-direction: column; gap: 6px; border-top: 1px solid var(--line); }
.orders-lbl { font-size: 12px; color: var(--ink-3); font-weight: 600; }
.o-chip { flex: none; border: 1px solid var(--line); background: var(--card); border-radius: 999px; padding: 5px 10px; font-size: 12.5px; cursor: pointer; white-space: nowrap; }
.o-chip:hover { border-color: var(--blue-100); }
.o-chip.on { background: var(--blue); border-color: var(--blue); color: #fff; }
.ctx-link { color: var(--ink); }
.composer { display: flex; gap: 8px; align-items: flex-end; padding: 12px; }
textarea { flex: 1; resize: none; min-height: 44px; max-height: 140px; border: 1.5px solid var(--line); border-radius: 14px; padding: 11px 12px; font: inherit; field-sizing: content; }
textarea:focus { border-color: var(--blue); outline: none; }
.send { width: 44px; height: 44px; border-radius: 50%; border: 0; background: var(--blue); color: #fff; cursor: pointer; display: grid; place-items: center; flex: none; }
.send:disabled { background: #c9d3e2; cursor: default; }
@media (max-width: 640px) { .drawer { width: 100%; } }
</style>
