<script setup lang="ts">
import { ref, watch } from 'vue'
import { api, type OrderDetail, type OrderStatus } from '../../api'
import Modal from '../../components/Modal.vue'
import PlatformIcon from '../../components/PlatformIcon.vue'
import { count, dateTime, deliveryLabel, languageLabel, money, nextStatuses, paymentLabel, statusLabel } from '../../format'
import { useLookups } from '../../store'

const props = defineProps<{ id: number }>()
const emit = defineEmits<{ close: []; changed: [] }>()
const { lookups } = useLookups()

const order = ref<OrderDetail | null>(null)
const busy = ref(false)
const error = ref('')

watch(() => props.id, async id => {
  order.value = null
  order.value = await api.order(id)
}, { immediate: true })

async function setStatus(s: OrderStatus) {
  busy.value = true
  error.value = ''
  try {
    order.value = await api.setStatus(props.id, s)
    emit('changed')
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busy.value = false
  }
}

async function setEmployee(ev: Event) {
  const v = (ev.target as HTMLSelectElement).value
  order.value = await api.setEmployee(props.id, v ? Number(v) : null)
  emit('changed')
}
</script>

<template>
  <Modal :title="`Заказ #${id}`" side width="480px" @close="emit('close')">
    <template #head>
      <span v-if="order" class="badge" :class="order.status">{{ statusLabel[order.status] }}</span>
    </template>

    <div v-if="!order" class="skeleton" style="height: 400px" />
    <div v-else class="stack">
      <div v-if="error" class="error-banner">{{ error }}</div>

      <section v-if="nextStatuses[order.status].length" class="actions">
        <span class="label">Сменить статус</span>
        <div class="row">
          <button v-for="s in nextStatuses[order.status]" :key="s" class="btn btn-sm"
                  :class="s === 'Cancelled' ? 'btn-danger' : 'btn-primary'" :disabled="busy" @click="setStatus(s)">
            {{ statusLabel[s] }}
          </button>
        </div>
      </section>

      <section class="kv">
        <div><span>Создан</span><b class="num">{{ dateTime(order.createdAt) }}</b></div>
        <div><span>Платформа</span><PlatformIcon :platform="order.platform" show-label /></div>
        <div><span>Оплата</span><b>{{ paymentLabel[order.paymentMethod] }}</b></div>
        <div><span>Получение</span><b>{{ deliveryLabel[order.deliveryType] }}</b></div>
        <div><span>Филиал</span><b>{{ order.branch.name }}</b></div>
        <div>
          <span>Сотрудник</span>
          <select class="select sm" :value="order.employee?.id ?? ''" @change="setEmployee">
            <option value="">Не назначен</option>
            <option v-for="e in lookups?.employees" :key="e.id" :value="e.id">{{ e.name }} · {{ e.role }}</option>
          </select>
        </div>
      </section>

      <section class="box">
        <h3>Клиент</h3>
        <div class="row" style="justify-content: space-between">
          <div>
            <b>{{ order.customer.fullName }}</b>
            <div class="muted num">{{ order.customer.phone }}<span v-if="order.customer.username"> · @{{ order.customer.username }}</span></div>
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
        <h3>Уведомления клиенту <span class="faint small">(автоответчик)</span></h3>
        <ul v-if="order.notifications.length" class="notes">
          <li v-for="n in order.notifications" :key="n.id">
            <div class="bubble">{{ n.text }}</div>
            <div class="faint small">{{ dateTime(n.sentAt) }} · {{ n.channel }} · {{ languageLabel[n.language] ?? n.language }}</div>
          </li>
        </ul>
        <p v-else class="faint small">Пока нет. Уведомление уйдёт при смене статуса.</p>
      </section>
    </div>
  </Modal>
</template>

<style scoped>
.stack { display: flex; flex-direction: column; gap: 16px; }
.label { display: block; font-size: 12px; font-weight: 600; color: var(--text-2); margin-bottom: 6px; }
.actions { padding: 12px; background: var(--plum-50); border-radius: 10px; }
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
