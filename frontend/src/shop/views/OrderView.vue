<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { ApiError, shopApi, type ShopOrder } from '../api'
import CancelDialog from '../components/CancelDialog.vue'
import ProductImage from '../components/ProductImage.vue'
import SIcon from '../components/SIcon.vue'
import { dateTime, lang, price, t } from '../i18n'
import { stepIndex, steps, statusText, tone } from '../orderStatus'
import { requireLogin, signedIn } from '../state/auth'
import { openChat } from '../state/chat'

// One order: progress, what's in it, how it's received, totals, and "Отменить заказ" while still possible.
const route = useRoute()
const id = computed(() => Number(route.params.id))
const placed = computed(() => route.query.placed === '1')
const order = ref<ShopOrder | null>(null)
const notFound = ref(false)
const cancelOpen = ref(false)

async function load() {
  if (!signedIn.value) return
  try {
    order.value = await shopApi.order(id.value)
  } catch (e) {
    if (e instanceof ApiError && e.status === 404) notFound.value = true
  }
}
watch([id, lang], load)
let timer: number | undefined
onMounted(() => {
  requireLogin(load)
  // The merchant moves the order along in the admin; pick that up without a reload.
  timer = window.setInterval(() => { if (document.visibilityState === 'visible') load() }, 15000)
})
onBeforeUnmount(() => clearInterval(timer))
watch(signedIn, s => s && load())

const current = computed(() => (order.value ? stepIndex(order.value.status, order.value.deliveryType) : -1))
</script>

<template>
  <div class="s-wrap narrow">
    <nav class="crumbs"><RouterLink to="/profile/orders"><SIcon name="chevronLeft" :size="16" /> {{ t('myOrders') }}</RouterLink></nav>

    <div v-if="!signedIn" class="s-empty">
      <h3>{{ t('signInToSee') }}</h3>
      <button class="s-btn" @click="requireLogin(load)">{{ t('signIn') }}</button>
    </div>
    <div v-else-if="notFound" class="s-empty"><h3>404</h3></div>

    <template v-else-if="order">
      <section v-if="placed && order.status !== 'Cancelled'" class="placed">
        <span class="ok">✓</span>
        <div>
          <b>{{ t('orderPlaced') }}</b>
          <p>{{ t('orderPlacedHint') }}</p>
        </div>
      </section>

      <header class="head">
        <div>
          <h1>{{ t('order') }} №{{ order.id }}</h1>
          <div class="faint">{{ t('created') }} {{ dateTime(order.createdAt) }}</div>
        </div>
        <span class="st-badge" :class="tone(order.status)">{{ statusText(order.status, order.deliveryType) }}</span>
      </header>

      <!-- Progress -->
      <ol v-if="order.status !== 'Cancelled'" class="track">
        <li v-for="(s, n) in steps(order.deliveryType)" :key="s" :class="{ done: n < current, now: n === current }">
          <span class="dot"><SIcon v-if="n < current" name="check" :size="14" /></span>
          <span class="lbl">{{ statusText(s, order.deliveryType) }}</span>
        </li>
      </ol>
      <p v-else class="cancelled">{{ order.cancelReason ?? t('st_Cancelled') }} · {{ dateTime(order.statusChangedAt) }}</p>

      <div class="grid">
        <section class="card">
          <h2>{{ t('receive') }}</h2>
          <div class="row">
            <SIcon :name="order.deliveryType === 'Pickup' ? 'store' : 'box'" class="ic" />
            <div>
              <b>{{ order.deliveryType === 'Pickup' ? t('pickup') : t('delivery') }}</b>
              <div v-if="order.deliveryType === 'Pickup'">{{ order.branch.name }} — {{ order.branch.address }}</div>
              <div v-else>{{ order.address }}</div>
            </div>
          </div>
          <h2>{{ t('recipient') }}</h2>
          <div class="row"><SIcon name="chat" class="ic" /><div><b>{{ order.recipientName }}</b><div>{{ order.recipientPhone }}</div></div></div>
          <h2>{{ t('payment') }}</h2>
          <div class="row"><span class="ic">💵</span><div>{{ t('cash') }}</div></div>
          <template v-if="order.comment">
            <h2>{{ t('comment') }}</h2>
            <p class="comment">{{ order.comment }}</p>
          </template>
        </section>

        <section class="card">
          <h2>{{ t('itemsInOrder') }}</h2>
          <ul class="items">
            <li v-for="i in order.items" :key="i.productId + (i.variant ?? '')">
              <RouterLink :to="`/product/${i.productId}`" class="th"><ProductImage :id="i.productId" :name="i.name" :src="i.image" /></RouterLink>
              <div class="grow">
                <RouterLink :to="`/product/${i.productId}`" class="nm">{{ i.name }}</RouterLink>
                <div class="faint small">{{ i.variant ? i.variant + ' · ' : '' }}{{ i.qty }} × {{ price(i.price) }}</div>
              </div>
              <b>{{ price(i.sum) }}</b>
            </li>
          </ul>
          <dl>
            <dt>{{ t('goods') }} ({{ order.itemsCount }})</dt><dd>{{ price(order.subtotal) }}</dd>
            <template v-if="order.promoDiscount"><dt>{{ t('promo') }} {{ order.promoCode }}</dt><dd class="green">−{{ price(order.promoDiscount) }}</dd></template>
            <template v-if="order.deliveryType === 'Delivery'"><dt>{{ t('deliveryFee') }}</dt><dd>{{ order.deliveryCost ? price(order.deliveryCost) : t('free') }}</dd></template>
          </dl>
          <div class="total"><span>{{ t('total') }}</span><strong>{{ price(order.total) }}</strong></div>
        </section>
      </div>

      <p v-if="order.canCancel" class="rule">{{ t('cancelWhileNew') }}</p>
      <p v-else-if="order.status !== 'Completed' && order.status !== 'Cancelled'" class="rule">{{ t('acceptedNoCancel') }}</p>
      <div class="actions">
        <button type="button" class="s-btn ghost" @click="openChat(null, order.id)"><SIcon name="chat" :size="18" /> {{ t('byChat') }}</button>
        <RouterLink v-if="placed" to="/" class="s-btn ghost">{{ t('continueShopping') }}</RouterLink>
        <button v-if="order.canCancel" type="button" class="cancel" @click="cancelOpen = true">{{ t('cancelOrder') }}</button>
      </div>
    </template>

    <div v-else class="s-skel" style="height: 420px; margin-top: 20px" />

    <CancelDialog v-if="cancelOpen && order" :order="order" @close="cancelOpen = false" @done="o => { order = o; cancelOpen = false }" />
  </div>
</template>

<style scoped>
.narrow { max-width: 960px; }
.crumbs { padding: 18px 0 6px; font-size: 14px; }
.crumbs a { display: inline-flex; align-items: center; gap: 2px; color: var(--ink-2); }
.placed { display: flex; gap: 14px; align-items: center; background: var(--green-50); border-radius: 18px; padding: 16px 18px; margin: 8px 0 16px; }
.placed .ok { width: 42px; height: 42px; border-radius: 50%; background: var(--green); color: #fff; display: grid; place-items: center; font-size: 22px; font-weight: 700; flex: none; }
.placed b { font-size: 17px; }
.placed p { margin: 2px 0 0; color: var(--ink-2); }
.head { display: flex; justify-content: space-between; align-items: flex-start; gap: 12px; flex-wrap: wrap; }
h1 { font-size: 28px; letter-spacing: -0.025em; margin: 0; }
.faint { color: var(--ink-3); }
.small { font-size: 13px; }
.st-badge { font-size: 14px; font-weight: 650; padding: 6px 12px; border-radius: 999px; }
.st-badge.amber { background: #fff4de; color: #b06a00; }
.st-badge.blue { background: var(--blue-50); color: var(--blue-600); }
.st-badge.green { background: var(--green-50); color: #0d7a45; }
.st-badge.grey { background: var(--card); color: var(--ink-3); }
.track { list-style: none; display: flex; margin: 20px 0; padding: 18px 12px; background: var(--card); border-radius: 18px; box-shadow: var(--lift); }
.track li { flex: 1; display: flex; flex-direction: column; align-items: center; gap: 8px; position: relative; text-align: center; font-size: 13px; color: var(--ink-3); }
.track li::before { content: ''; position: absolute; top: 13px; left: -50%; width: 100%; height: 3px; background: var(--line); z-index: 0; }
.track li:first-child::before { display: none; }
.track li.done::before, .track li.now::before { background: var(--blue); }
.dot { width: 28px; height: 28px; border-radius: 50%; background: var(--line); display: grid; place-items: center; color: #fff; z-index: 1; }
.done .dot { background: var(--blue); }
.now .dot { background: var(--card); border: 3px solid var(--blue); box-shadow: 0 0 0 4px var(--blue-50); }
.now .lbl { color: var(--ink); font-weight: 650; }
.done .lbl { color: var(--ink-2); }
.cancelled { margin: 16px 0; padding: 12px 16px; background: var(--card); border-radius: 14px; color: var(--ink-2); }
.grid { display: grid; grid-template-columns: 1fr 1.2fr; gap: 14px; align-items: start; }
.card { background: var(--card); border-radius: 20px; padding: 18px; box-shadow: var(--lift); display: flex; flex-direction: column; gap: 10px; }
.card h2 { font-size: 14px; color: var(--ink-3); text-transform: uppercase; letter-spacing: .04em; margin: 6px 0 0; }
.card h2:first-child { margin-top: 0; }
.row { display: flex; gap: 12px; align-items: flex-start; font-size: 14.5px; }
.ic { color: var(--blue); flex: none; width: 22px; text-align: center; }
.comment { margin: 0; color: var(--ink-2); }
.items { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 10px; }
.items li { display: flex; align-items: center; gap: 12px; }
.th { width: 52px; height: 52px; border-radius: 12px; overflow: hidden; flex: none; }
.th :deep(.ph) { font-size: 26px; }
.grow { flex: 1; min-width: 0; }
.nm { font-weight: 550; }
.nm:hover { color: var(--blue); }
dl { display: grid; grid-template-columns: 1fr auto; gap: 6px; margin: 6px 0 0; font-size: 14.5px; border-top: 1px solid var(--line); padding-top: 12px; }
dt { color: var(--ink-2); }
dd { margin: 0; text-align: right; }
.green { color: var(--green); font-weight: 600; }
.total { display: flex; justify-content: space-between; align-items: baseline; }
.total strong { font-size: 22px; }
.rule { margin: 16px 0 0; font-size: 13.5px; color: var(--ink-3); }
.actions { display: flex; gap: 10px; align-items: center; margin-top: 16px; flex-wrap: wrap; }
.cancel { margin-left: auto; border: 0; background: none; color: var(--red); font-weight: 600; cursor: pointer; font-size: 15px; }
@media (max-width: 760px) {
  .grid { grid-template-columns: 1fr; }
  h1 { font-size: 23px; }
  .track li { font-size: 11px; }
}
</style>
