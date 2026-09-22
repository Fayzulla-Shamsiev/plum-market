<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { shopApi, type Quote } from '../api'
import HeartButton from '../components/HeartButton.vue'
import ProductImage from '../components/ProductImage.vue'
import SIcon from '../components/SIcon.vue'
import { useRouter } from 'vue-router'
import { lang, price, t } from '../i18n'
import { requireLogin } from '../state/auth'
import { allSelected, cartCount, cartLines, removeLines, removeSelected, selectAll, selectedLines, setQty, toggleSelected } from '../state/cart'

// Cart: lines are kept in the browser; the server re-prices them (current price, discount, stock) on every change.
const quote = ref<Quote | null>(null)
const failed = ref(false)
const capped = ref<Set<string>>(new Set())
const key = (id: number, variant: string | null) => `${id}|${variant ?? ''}`

let timer: number | undefined
let seq = 0
function requote() {
  clearTimeout(timer)
  timer = window.setTimeout(async () => {
    const mine = ++seq
    try {
      const q = await shopApi.quote(cartLines.value.map(l => ({ productId: l.id, variant: l.variant, qty: l.qty })))
      if (mine !== seq) return
      failed.value = false
      // Stock went down since the item was added: cap the saved quantity and say so.
      for (const l of q.lines) {
        const local = cartLines.value.find(x => x.id === l.productId && (x.variant ?? null) === (l.variant ?? null))
        if (local && l.available && l.qty < local.qty) {
          capped.value.add(key(l.productId, l.variant))
          local.qty = l.qty
        }
      }
      quote.value = q
    } catch {
      failed.value = true
    }
  }, 150)
}
watch([cartLines, lang], requote, { deep: true, immediate: true })

const lines = computed(() => quote.value?.lines ?? [])
const unavailable = computed(() => lines.value.filter(l => !l.available))
function inc(l: Quote['lines'][number]) {
  if (l.maxQty !== null && l.qty >= l.maxQty) return
  setQty(l.productId, l.variant, l.qty + 1, l.maxQty)
}
const dec = (l: Quote['lines'][number]) => l.qty > 1 && setQty(l.productId, l.variant, l.qty - 1, l.maxQty)
const remove = (l: Quote['lines'][number]) => removeLines([{ id: l.productId, variant: l.variant }])
const clearUnavailable = () => removeLines(unavailable.value.map(l => ({ id: l.productId, variant: l.variant })))

// "Ваш заказ" counts only the ticked lines — those are what goes to checkout.
const isOn = (l: Quote['lines'][number]) => selectedLines.value.some(x => x.id === l.productId && (x.variant ?? null) === (l.variant ?? null))
const chosen = computed(() => lines.value.filter(l => l.available && isOn(l)))
const summary = computed(() => {
  const subtotal = chosen.value.reduce((s, l) => s + l.oldSum, 0)
  const total = chosen.value.reduce((s, l) => s + l.sum, 0)
  return { count: chosen.value.reduce((s, l) => s + l.qty, 0), subtotal, discount: subtotal - total, total }
})
const selectedCount = computed(() => selectedLines.value.length)
const router = useRouter()
const checkout = () => requireLogin(() => router.push('/checkout'))
</script>

<template>
  <div class="s-wrap">
    <header class="head">
      <h1>{{ t('cart') }}</h1>
      <span v-if="cartCount" class="count">{{ cartCount }} {{ t('pcs') }}</span>
    </header>

    <div v-if="!cartLines.length" class="s-empty">
      <div class="big">🧺</div>
      <h3>{{ t('cartEmpty') }}</h3>
      <p>{{ t('cartEmptyHint') }}</p>
      <RouterLink to="/catalog" class="s-btn">{{ t('toCatalog') }}</RouterLink>
    </div>

    <div v-else-if="failed && !quote" class="s-empty">
      <h3>{{ t('error') }}</h3>
      <button class="s-btn ghost" @click="requote">{{ t('retry') }}</button>
    </div>

    <div v-else class="layout">
      <section class="lines">
        <div class="bar">
          <label class="all">
            <input type="checkbox" class="s-check" :checked="allSelected" @change="selectAll(($event.target as HTMLInputElement).checked)" />
            {{ t('selectAll') }}
          </label>
          <button type="button" class="link danger" :disabled="!selectedCount" @click="removeSelected">
            <SIcon name="trash" :size="16" /> {{ t('deleteSelected') }}<template v-if="selectedCount"> ({{ selectedCount }})</template>
          </button>
        </div>
        <article v-for="l in lines.filter(x => x.available)" :key="key(l.productId, l.variant)" class="line" :class="{ off: !isOn(l) }">
          <input type="checkbox" class="s-check pick" :checked="isOn(l)" :aria-label="l.card!.name" @change="toggleSelected(l.productId, l.variant)" />
          <RouterLink :to="`/product/${l.productId}`" class="thumb"><ProductImage :id="l.productId" :name="l.card!.name" :src="l.card!.image" /></RouterLink>
          <div class="main">
            <RouterLink :to="`/product/${l.productId}`" class="name">{{ l.card!.name }}</RouterLink>
            <div v-if="l.variant" class="variant">{{ l.variant }}</div>
            <div class="unit">
              <b :class="{ sale: l.oldPrice }">{{ price(l.price) }}</b>
              <s v-if="l.oldPrice">{{ price(l.oldPrice) }}</s>
              <span v-if="l.card!.discountPercent" class="pct">−{{ l.card!.discountPercent }}%</span>
            </div>
            <div v-if="capped.has(key(l.productId, l.variant))" class="warn">{{ t('qtyCapped') }}: {{ l.maxQty }}</div>
            <div class="tools">
              <HeartButton :id="l.productId" :size="18" class="icon-btn" />
              <button type="button" class="icon-btn del" :aria-label="t('remove')" :title="t('remove')" @click="remove(l)"><SIcon name="trash" :size="18" /></button>
            </div>
          </div>
          <div class="right">
            <div class="stepper">
              <button type="button" :disabled="l.qty <= 1" aria-label="−1" @click="dec(l)"><SIcon name="minus" :size="16" /></button>
              <output>{{ l.qty }}</output>
              <button type="button" :disabled="l.maxQty !== null && l.qty >= l.maxQty" aria-label="+1" @click="inc(l)"><SIcon name="plus" :size="16" /></button>
            </div>
            <div class="sum">{{ price(l.sum) }}</div>
            <s v-if="l.oldSum > l.sum" class="old-sum">{{ price(l.oldSum) }}</s>
          </div>
        </article>

        <div v-if="unavailable.length" class="gone">
          <div class="gone-head">
            <b>{{ t('unavailable') }}</b>
            <button type="button" class="link" @click="clearUnavailable">{{ t('remove') }}</button>
          </div>
          <div v-for="l in unavailable" :key="key(l.productId, l.variant)" class="gone-row">
            {{ l.card?.name ?? `#${l.productId}` }}<span v-if="l.variant"> · {{ l.variant }}</span>
          </div>
        </div>
      </section>

      <aside v-if="quote" class="summary">
        <h2>{{ t('yourOrder') }}</h2>
        <dl>
          <dt>{{ t('goods') }} ({{ summary.count }})</dt><dd>{{ price(summary.subtotal) }}</dd>
          <template v-if="summary.discount"><dt>{{ t('discount') }}</dt><dd class="green">−{{ price(summary.discount) }}</dd></template>
        </dl>
        <div class="total"><span>{{ t('total') }}</span><strong>{{ price(summary.total) }}</strong></div>
        <button type="button" class="s-btn checkout" :disabled="!summary.count" @click="checkout">{{ t('checkout') }}</button>
        <p v-if="!summary.count" class="soon">{{ t('nothingSelected') }}</p>
      </aside>
    </div>
  </div>
</template>

<style scoped>
.head { display: flex; align-items: baseline; gap: 12px; padding: 22px 0 16px; }
h1 { font-size: 30px; letter-spacing: -0.025em; margin: 0; }
.count { color: var(--ink-3); }
.s-empty p { margin: 0 0 18px; }
.layout { display: grid; grid-template-columns: minmax(0, 1fr) 340px; gap: 24px; align-items: start; }
.lines { display: flex; flex-direction: column; gap: 12px; }
.bar { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 8px 12px; background: var(--card); border-radius: 16px; padding: 12px 16px; box-shadow: var(--lift); }
.all { display: flex; align-items: center; gap: 10px; font-weight: 600; cursor: pointer; white-space: nowrap; }
.link.danger:disabled { color: var(--ink-3); cursor: default; }
.link.danger { display: inline-flex; align-items: center; gap: 6px; color: var(--red); }
.pick { align-self: center; }
.line.off .thumb, .line.off .main, .line.off .right { opacity: .5; }
.line { display: grid; grid-template-columns: 20px 104px minmax(0, 1fr) auto; gap: 16px; background: var(--card); border-radius: 18px; padding: 14px; box-shadow: var(--lift); }
.thumb { border-radius: 14px; overflow: hidden; align-self: start; }
.main { display: flex; flex-direction: column; gap: 3px; min-width: 0; }
.name { font-weight: 600; line-height: 1.35; }
.name:hover { color: var(--blue); }
.variant { font-size: 13px; color: var(--ink-2); }
.unit { display: flex; align-items: baseline; gap: 8px; font-size: 14px; margin-top: 2px; }
.unit .sale { color: var(--green); }
.unit s { color: var(--ink-3); font-size: 12.5px; }
.pct { font-size: 12px; font-weight: 700; color: var(--green); }
.warn { font-size: 12.5px; color: #c77700; }
.tools { display: flex; gap: 6px; margin-top: auto; padding-top: 6px; }
.icon-btn { width: 36px; height: 36px; border-radius: 10px; border: 0; background: var(--page); box-shadow: none; color: var(--ink-2); display: grid; place-items: center; cursor: pointer; }
.icon-btn:hover { color: var(--red); }
.right { display: flex; flex-direction: column; align-items: flex-end; gap: 8px; }
.stepper { display: grid; grid-template-columns: 36px 40px 36px; align-items: center; height: 38px; border-radius: 11px; background: var(--page); }
.stepper button { height: 100%; border: 0; background: none; cursor: pointer; display: grid; place-items: center; color: var(--ink); border-radius: 11px; }
.stepper button:hover:not(:disabled) { background: var(--line); }
.stepper button:disabled { color: var(--ink-3); cursor: default; opacity: .5; }
.stepper output { text-align: center; font-weight: 700; font-variant-numeric: tabular-nums; }
.sum { font-weight: 750; font-size: 17px; white-space: nowrap; }
.old-sum { color: var(--ink-3); font-size: 13px; }
.gone { background: var(--card); border-radius: 18px; padding: 14px 16px; border: 1px dashed var(--line); }
.gone-head { display: flex; justify-content: space-between; margin-bottom: 6px; }
.gone-row { color: var(--ink-3); font-size: 14px; text-decoration: line-through; }
.link { border: 0; background: none; color: var(--blue); cursor: pointer; font-weight: 600; }

.summary { position: sticky; top: 150px; background: var(--card); border-radius: 20px; padding: 20px; box-shadow: var(--lift); display: flex; flex-direction: column; gap: 12px; }
.summary h2 { font-size: 19px; margin: 0; }
dl { display: grid; grid-template-columns: 1fr auto; gap: 8px; margin: 0; font-size: 14.5px; }
dt { color: var(--ink-2); }
dd { margin: 0; text-align: right; }
.green { color: var(--green); font-weight: 600; }
.total { display: flex; justify-content: space-between; align-items: baseline; padding-top: 12px; border-top: 1px solid var(--line); }
.total strong { font-size: 24px; letter-spacing: -0.02em; }
.checkout { width: 100%; height: 50px; }
.checkout:disabled { opacity: .55; cursor: not-allowed; }
.soon { margin: 0; font-size: 13px; color: var(--ink-3); text-align: center; }

@media (max-width: 900px) {
  .layout { grid-template-columns: 1fr; }
  .summary { position: static; }
}
@media (max-width: 640px) {
  h1 { font-size: 24px; }
  .line { grid-template-columns: 20px 76px minmax(0, 1fr); gap: 12px; padding: 12px; }
  .pick { align-self: start; margin-top: 4px; }
  .right { grid-column: 2 / -1; flex-direction: row; align-items: center; justify-content: space-between; }
  .old-sum { display: none; }
}
</style>
