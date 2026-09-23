<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { api, type OrderDetail } from '../../api'
import Modal from '../../components/Modal.vue'
import { count, dateTime, deliveryLabel, languageLabel, money, statusText, stepText } from '../../format'

// Order card for the store: where the order is in the flow (with times), the one next step, and cancellation.
const props = defineProps<{ id: number }>()
const emit = defineEmits<{ close: []; changed: [] }>()

const order = ref<OrderDetail | null>(null)
const busy = ref(false)
const error = ref('')
const cancelling = ref(false)
const reason = ref('')

watch(() => props.id, async id => {
  order.value = null
  cancelling.value = false
  order.value = await api.order(id)
}, { immediate: true })

async function run(fn: () => Promise<OrderDetail>) {
  busy.value = true
  error.value = ''
  try {
    order.value = await fn()
    emit('changed')
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busy.value = false
  }
}
const advance = () => order.value?.next && run(() => api.setStatus(props.id, order.value!.next!))
async function cancel() {
  await run(() => api.setStatus(props.id, 'Cancelled', reason.value.trim() || undefined))
  cancelling.value = false
  reason.value = ''
}

// Timeline: every step of this order's flow, with the time it happened (if it did).
const timeline = computed(() => {
  const o = order.value
  if (!o) return []
  const at = (s: string) => o.history.filter(h => h.status === s).at(-1)
  const steps = o.status === 'Cancelled' ? [...o.history.map(h => h.status)] : o.steps
  const current = steps.indexOf(o.status)
  return steps.map((s, i) => ({ status: s, done: i <= current, now: i === current, entry: at(s) }))
})
</script>

<template>
  <Modal :title="`Заказ #${id}`" side width="500px" @close="emit('close')">
    <template #head>
      <span v-if="order" class="badge" :class="order.status">{{ statusText(order.status, order.deliveryType) }}</span>
      <span v-if="order?.overdue" class="badge Overdue">просрочен</span>
    </template>

    <div v-if="!order" class="skeleton" style="height: 400px" />
    <div v-else class="stack">
      <div v-if="error" class="error-banner">{{ error }}</div>

      <!-- Delivery control -->
      <section class="flow">
        <ol class="steps">
          <li v-for="t in timeline" :key="t.status" :class="{ done: t.done, now: t.now, cancelled: t.status === 'Cancelled' }">
            <span class="dot" />
            <span class="st">{{ statusText(t.status, order.deliveryType) }}</span>
            <span v-if="t.entry" class="when">{{ dateTime(t.entry.at) }} · {{ t.entry.by }}</span>
          </li>
        </ol>
        <div v-if="order.next || order.canCancel" class="actions">
          <button v-if="order.next" class="btn btn-primary" :class="{ final: order.next === 'Completed' }" :disabled="busy" @click="advance">
            {{ stepText(order.next, order.deliveryType) }} →
          </button>
          <button v-if="order.canCancel && !cancelling" class="btn btn-ghost danger" :disabled="busy" @click="cancelling = true">Отменить заказ</button>
        </div>
        <div v-if="cancelling" class="cancel-box">
          <label class="field"><span>Причина отмены (увидит покупатель)</span>
            <input v-model="reason" class="input" maxlength="300" placeholder="Например: нет в наличии" />
          </label>
          <div class="row">
            <button class="btn btn-danger btn-sm" :disabled="busy" @click="cancel">Отменить заказ</button>
            <button class="btn btn-ghost btn-sm" @click="cancelling = false">Не отменять</button>
          </div>
        </div>
        <p v-if="order.status === 'New'" class="faint small note">Пока заказ «Новый», покупатель может отменить его сам. После «Принять в сборку» — только магазин.</p>
      </section>

      <section class="kv">
        <div><span>Создан</span><b class="num">{{ dateTime(order.createdAt) }}</b></div>
        <div><span>Получение</span><b>{{ deliveryLabel[order.deliveryType] }}</b></div>
        <div><span>Филиал</span><b>{{ order.branch.name }}</b></div>
        <div><span>Оплата</span><b>Наличными при получении</b></div>
      </section>

      <section class="box">
        <h3>Клиент</h3>
        <div class="row" style="justify-content: space-between">
          <div>
            <b>{{ order.customer.fullName }}</b>
            <div class="muted num">{{ order.customer.phone }}<span v-if="order.customer.email"> · {{ order.customer.email }}</span></div>
          </div>
          <div class="right small">
            <div class="faint">Бонусы</div>
            <b class="num">{{ count(order.customer.bonusPoints) }}</b>
          </div>
        </div>
        <p v-if="order.recipientName && (order.recipientName !== order.customer.fullName || order.recipientPhone !== order.customer.phone)" class="addr">
          👤 Получатель: <b>{{ order.recipientName }}</b>, <span class="num">{{ order.recipientPhone }}</span>
        </p>
        <p v-if="order.address" class="addr">📍 {{ order.address }}</p>
        <p v-if="order.cancelReason" class="comment">✖ {{ order.cancelReason }}</p>
        <p v-if="order.comment" class="comment">💬 {{ order.comment }}</p>
      </section>

      <section class="box">
        <h3>Состав заказа</h3>
        <table class="items">
          <tr v-for="(i, idx) in order.items" :key="idx">
            <td>{{ i.productName }}</td>
            <td class="num faint nowrap">{{ i.quantity }} × {{ money(i.price) }}</td>
            <td class="num right nowrap">{{ money(i.sum) }}</td>
          </tr>
        </table>
        <div class="totals">
          <div><span>Товары</span><span class="num">{{ money(order.subtotal) }}</span></div>
          <div v-if="order.promoDiscount"><span>Промокод {{ order.promoCode }}</span><span class="num">−{{ money(order.promoDiscount) }}</span></div>
          <div><span>Доставка</span><span class="num">{{ order.deliveryCost ? money(order.deliveryCost) : 'бесплатно' }}</span></div>
          <div class="grand"><span>Итого</span><span class="num">{{ money(order.total) }}</span></div>
          <div v-if="order.bonusEarned" class="faint"><span>Начислено бонусов</span><span class="num">+{{ count(order.bonusEarned) }}</span></div>
        </div>
      </section>

      <section class="box">
        <h3>Сообщения покупателю <span class="faint small">(приходят в его чат с магазином)</span></h3>
        <ul v-if="order.notifications.length" class="notes">
          <li v-for="n in order.notifications" :key="n.id">
            <div class="bubble">{{ n.text }}</div>
            <div class="faint small">{{ dateTime(n.sentAt) }} · {{ languageLabel[n.language] ?? n.language }}</div>
          </li>
        </ul>
        <p v-else class="faint small">Пока нет. Сообщение уйдёт при смене статуса (если покупатель не отключил их в настройках).</p>
      </section>
    </div>
  </Modal>
</template>

<style scoped>
.stack { display: flex; flex-direction: column; gap: 16px; }
.flow { padding: 14px; background: var(--plum-50); border-radius: 12px; display: flex; flex-direction: column; gap: 12px; }
.steps { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 0; }
.steps li { display: grid; grid-template-columns: 18px 1fr auto; align-items: center; gap: 8px; padding: 5px 0; position: relative; color: var(--text-3); }
.steps li::before { content: ''; position: absolute; left: 6px; top: -9px; height: 18px; width: 2px; background: var(--border-strong); }
.steps li:first-child::before { display: none; }
.steps li.done::before { background: var(--plum-500); }
.dot { width: 14px; height: 14px; border-radius: 50%; border: 2px solid var(--border-strong); background: var(--surface); z-index: 1; }
.done .dot { background: var(--plum-600); border-color: var(--plum-600); }
.now .dot { box-shadow: 0 0 0 4px rgb(131 64 168 / 20%); }
.done .st { color: var(--text); }
.now .st { font-weight: 700; }
.cancelled .dot { background: var(--bad); border-color: var(--bad); }
.cancelled .st { color: var(--bad); }
.when { font-size: 12px; color: var(--text-3); white-space: nowrap; }
.actions { display: flex; gap: 8px; flex-wrap: wrap; }
.actions .final { background: var(--good); border-color: var(--good); }
.danger { color: var(--bad); }
.cancel-box { display: flex; flex-direction: column; gap: 8px; padding: 10px; background: var(--surface); border-radius: 10px; border: 1px solid #f1b7b7; }
.note { margin: 0; }
.kv { display: grid; grid-template-columns: 1fr 1fr; gap: 12px 16px; }
.kv > div { display: flex; flex-direction: column; gap: 2px; }
.kv span:first-child { font-size: 12px; color: var(--text-3); }
.select.sm { height: 30px; font-size: 13px; }
.box { border: 1px solid var(--border); border-radius: 10px; padding: 12px 14px; }
.box h3 { margin-bottom: 8px; }
.addr, .comment { margin: 8px 0 0; font-size: 13px; color: var(--text-2); }
.items { width: 100%; border-collapse: collapse; }
.items td { padding: 5px 0; border-bottom: 1px dashed var(--border); }
.items td + td { padding-left: 8px; }
.right { text-align: right; }
.totals { margin-top: 8px; display: flex; flex-direction: column; gap: 3px; }
.totals div { display: flex; justify-content: space-between; }
.totals .grand { font-weight: 700; font-size: 15px; margin-top: 4px; }
.notes { list-style: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: 10px; }
.bubble { background: #e7f3fd; border-radius: 12px 12px 12px 3px; padding: 8px 11px; margin-bottom: 3px; }
.small { font-size: 12px; font-weight: 400; }
</style>
