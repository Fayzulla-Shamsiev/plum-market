<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { shopApi, type OrdersPage, type ShopOrder } from '../api'
import CancelDialog from '../components/CancelDialog.vue'
import OrderCard from '../components/OrderCard.vue'
import SIcon from '../components/SIcon.vue'
import { lang, t } from '../i18n'
import { requireLogin, signedIn } from '../state/auth'
import { persisted } from '../state/persist'

// "Мои заказы": active and all orders. Refreshes on its own so status changes made in the admin show up.
const scope = persisted<'active' | 'all'>('plum.shop.ordersTab', 'active')
const data = ref<OrdersPage | null>(null)
const items = ref<ShopOrder[]>([])
const cancelling = ref<ShopOrder | null>(null)

async function load(page = 1) {
  const res = await shopApi.orders(scope.value, page)
  data.value = res
  items.value = page === 1 ? res.items : [...items.value, ...res.items]
}
const refresh = () => signedIn.value && load(1).catch(() => {})
watch([scope, lang], refresh)
let timer: number | undefined
onMounted(() => {
  requireLogin(refresh)
  timer = window.setInterval(() => { if (document.visibilityState === 'visible' && data.value?.page === 1) refresh() }, 20000)
})
onBeforeUnmount(() => clearInterval(timer))
watch(signedIn, s => s && refresh())

function onCancelled(o: ShopOrder) {
  cancelling.value = null
  if (scope.value === 'active') items.value = items.value.filter(x => x.id !== o.id)
  else items.value = items.value.map(x => (x.id === o.id ? o : x))
  refresh()
}
</script>

<template>
  <div class="s-wrap narrow">
    <nav class="crumbs"><RouterLink to="/profile"><SIcon name="chevronLeft" :size="16" /> {{ t('profile') }}</RouterLink></nav>
    <h1>{{ t('myOrders') }}</h1>

    <div v-if="!signedIn" class="s-empty">
      <div class="big">📦</div>
      <h3>{{ t('signInToSee') }}</h3>
      <button class="s-btn" @click="requireLogin(refresh)">{{ t('signIn') }}</button>
    </div>

    <template v-else>
      <div class="tabs" role="tablist">
        <button type="button" role="tab" :aria-selected="scope === 'active'" :class="{ on: scope === 'active' }" @click="scope = 'active'">
          {{ t('activeOrders') }} <small v-if="data">{{ data.activeCount }}</small>
        </button>
        <button type="button" role="tab" :aria-selected="scope === 'all'" :class="{ on: scope === 'all' }" @click="scope = 'all'">
          {{ t('allOrders') }} <small v-if="data">{{ data.allCount }}</small>
        </button>
      </div>

      <div v-if="data && !items.length" class="s-empty">
        <div class="big">📦</div>
        <h3>{{ scope === 'active' ? t('noActive') : t('noOrders') }}</h3>
        <RouterLink to="/catalog" class="s-btn">{{ t('toCatalog') }}</RouterLink>
      </div>
      <div v-else class="list">
        <OrderCard v-for="o in items" :key="o.id" :o="o" @cancel="cancelling = o" />
        <template v-if="!data"><div v-for="i in 3" :key="i" class="s-skel" style="height: 240px" /></template>
      </div>
      <div v-if="data && items.length < data.total" class="more">
        <button class="s-btn ghost" @click="load(data.page + 1)">{{ t('showMore') }}</button>
      </div>
    </template>

    <CancelDialog v-if="cancelling" :order="cancelling" @close="cancelling = null" @done="onCancelled" />
  </div>
</template>

<style scoped>
.narrow { max-width: 820px; }
.crumbs { padding: 18px 0 6px; font-size: 14px; }
.crumbs a { display: inline-flex; align-items: center; gap: 2px; color: var(--ink-2); }
h1 { font-size: 30px; letter-spacing: -0.025em; margin: 0 0 16px; }
.tabs { display: inline-flex; background: var(--card); border-radius: 14px; padding: 4px; box-shadow: var(--lift); margin-bottom: 16px; }
.tabs button { border: 0; background: none; padding: 9px 16px; border-radius: 10px; font-weight: 600; cursor: pointer; color: var(--ink-2); }
.tabs button.on { background: var(--ink); color: #fff; }
.tabs small { opacity: .6; margin-left: 4px; }
.list { display: flex; flex-direction: column; gap: 14px; }
.more { display: flex; justify-content: center; margin-top: 20px; }
@media (max-width: 640px) { h1 { font-size: 24px; } }
</style>
