<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { ApiError, shopApi, type Card } from '../api'
import type { ProductPage } from '../api'
import BuyButton from '../components/BuyButton.vue'
import Gallery from '../components/Gallery.vue'
import HeartButton from '../components/HeartButton.vue'
import ProductRail from '../components/ProductRail.vue'
import ReviewsBlock from '../components/ReviewsBlock.vue'
import SIcon from '../components/SIcon.vue'
import { count, lang, price, t, weight } from '../i18n'
import { qtyIn } from '../state/cart'
import { openChat } from '../state/chat'
import { markViewed, viewedIds } from '../state/viewed'

const route = useRoute()
const id = computed(() => Number(route.params.id))
const data = ref<ProductPage | null>(null)
const notFound = ref(false)
const failed = ref(false)
const variant = ref<string | null>(null)
const recommended = ref<Card[]>([])
const viewed = ref<Card[]>([])
const showBranches = ref(false)

async function load() {
  notFound.value = false
  failed.value = false
  try {
    const page = await shopApi.product(id.value)
    // Keep the chosen variant when only the language changed.
    if (data.value?.card.id !== page.card.id || !page.variants.some(v => v.name === variant.value))
      variant.value = page.variants[0]?.name ?? null
    data.value = page
    markViewed(page.card.id)
  } catch (e) {
    if (e instanceof ApiError && e.status === 404) { notFound.value = true; data.value = null }
    else failed.value = true
  }
}
async function loadRails() {
  // Viewed = what was opened before this product (the current one is already at the front of the list).
  const before = viewedIds.value.filter(x => x !== id.value).slice(0, 12)
  const [rec, seen] = await Promise.all([shopApi.recommended(id.value).catch(() => []), shopApi.cards(before).catch(() => [])])
  recommended.value = rec
  viewed.value = seen
}
watch([id, lang], () => { load(); loadRails() }, { immediate: true })
watch(id, () => { showBranches.value = false })

const card = computed(() => data.value?.card)
// Price shown in the buy box follows the selected variant.
const current = computed(() => {
  const d = data.value
  if (!d) return null
  const v = d.variants.find(x => x.name === variant.value)
  return v ? { price: v.price, oldPrice: v.oldPrice, percent: v.discountPercent } : { price: d.card.price, oldPrice: d.card.oldPrice, percent: d.card.discountPercent }
})
const inCart = computed(() => (card.value ? qtyIn(card.value.id, variant.value) : 0))
const paragraphs = computed(() => data.value?.description.split(/\n\s*\n/).map(x => x.trim()).filter(Boolean) ?? [])
const hasMoreText = computed(() => !!data.value && data.value.description.trim() !== data.value.shortDescription.trim())

const stockText = computed(() => {
  const d = data.value
  if (!d) return ''
  if (!d.card.inStock) return t('outOfStock')
  if (d.stock.unlimited) return t('inStockMany')
  return `${t('inStock')}: ${d.stock.quantity} ${t('pcs')}`
})
const lowStock = computed(() => !!data.value && !data.value.stock.unlimited && data.value.stock.quantity > 0 && data.value.stock.quantity <= 5)

const specs = computed(() => {
  const d = data.value
  if (!d) return []
  const rows: [string, string][] = []
  if (d.weightGrams) rows.push([t('weightL'), weight(d.weightGrams)])
  rows.push([t('unitL'), d.unit])
  if (d.lengthCm && d.widthCm && d.heightCm) rows.push([t('sizeL'), `${d.lengthCm} × ${d.widthCm} × ${d.heightCm} см`])
  for (const a of d.attributes) rows.push([a.name, a.value])
  if (d.tags.length) rows.push([t('tagsL'), d.tags.join(', ')])
  return rows
})

function ask() {
  const d = data.value
  if (!d) return
  openChat({ id: d.card.id, name: d.card.name, price: current.value?.price ?? d.card.price, image: d.card.image })
}
const scrollTo = (sel: string) => document.querySelector(sel)?.scrollIntoView({ behavior: 'smooth', block: 'start' })
const sinceYear = computed(() => (data.value?.seller.since ? new Date(data.value.seller.since).getFullYear() : null))
</script>

<template>
  <div class="s-wrap">
    <div v-if="notFound" class="s-empty">
      <div class="big">🔎</div>
      <h3>{{ t('notFound') }}</h3>
      <RouterLink to="/catalog" class="s-btn ghost">{{ t('toCatalog') }}</RouterLink>
    </div>
    <div v-else-if="failed" class="s-empty">
      <h3>{{ t('error') }}</h3>
      <button class="s-btn ghost" @click="load">{{ t('retry') }}</button>
    </div>

    <template v-else-if="data && card && current">
      <nav class="crumbs">
        <RouterLink to="/">{{ t('home') }}</RouterLink>
        <template v-for="c in data.trail" :key="c.id">
          <SIcon name="chevronRight" :size="14" />
          <RouterLink :to="`/catalog/${c.id}`">{{ c.name }}</RouterLink>
        </template>
      </nav>

      <div class="top">
        <Gallery :id="card.id" :name="card.name" :media="data.media">
          <span v-if="current.percent" class="disc">−{{ current.percent }}%</span>
        </Gallery>

        <div class="info">
          <h1>{{ card.name }}</h1>
          <div class="meta-row">
            <button v-if="card.reviewsCount" type="button" class="rating" @click="scrollTo('#reviews')">
              <SIcon name="star" :size="17" filled class="star" />
              <b>{{ card.rating.toFixed(1) }}</b>
              <span>{{ count(card.reviewsCount, 'reviews') }}</span>
            </button>
            <span v-else class="faint">{{ t('noReviews') }}</span>
            <span v-if="card.weightGrams" class="faint">· {{ weight(card.weightGrams) }}</span>
          </div>

          <p v-if="data.shortDescription" class="lead">
            {{ data.shortDescription }}
            <button v-if="hasMoreText || specs.length > 1" type="button" class="link" @click="scrollTo('#about')">{{ t('more') }}</button>
          </p>

          <div v-if="data.variants.length" class="variants">
            <div class="label">{{ t('variant') }}</div>
            <div class="seg" role="radiogroup" :aria-label="t('variant')">
              <button v-for="v in data.variants" :key="v.name" type="button" role="radio" :aria-checked="v.name === variant"
                :class="{ on: v.name === variant }" @click="variant = v.name">
                <span>{{ v.name }}</span>
                <small>{{ price(v.price) }}</small>
              </button>
            </div>
          </div>

          <!-- Buy box -->
          <section class="buybox">
            <div class="price-row">
              <strong :class="{ sale: current.oldPrice }">{{ price(current.price) }}</strong>
              <template v-if="current.oldPrice">
                <s>{{ price(current.oldPrice) }}</s>
                <span class="save">{{ t('save') }} {{ price(current.oldPrice - current.price) }}</span>
              </template>
            </div>
            <div class="stock" :class="{ low: lowStock, out: !card.inStock }">
              <SIcon :name="card.inStock ? 'box' : 'close'" :size="16" />
              {{ stockText }}
              <button type="button" class="link small" @click="showBranches = !showBranches">{{ t('byBranch') }}</button>
            </div>
            <ul v-if="showBranches" class="branches">
              <li v-for="b in data.stock.branches" :key="b.id" :class="{ no: b.status === 'None' || b.status === 'OutOfStock' }">
                <SIcon name="pin" :size="15" />
                <span><b>{{ b.name }}</b><small>{{ b.address }}</small></span>
                <em>{{ b.status === 'Unlimited' ? t('plenty') : b.status === 'Limited' ? `${b.quantity} ${t('pcs')}` : b.status === 'None' ? t('notSoldHere') : t('outOfStock') }}</em>
              </li>
            </ul>
            <div class="actions">
              <BuyButton :id="card.id" :variant="variant" :max="card.maxQty" :in-stock="card.inStock" size="lg" class="grow" />
              <HeartButton :id="card.id" label />
            </div>
            <RouterLink v-if="inCart" to="/cart" class="to-cart">{{ t('goToCart') }} <SIcon name="chevronRight" :size="16" /></RouterLink>
          </section>

          <!-- Seller -->
          <section class="seller">
            <span class="s-ava"><SIcon name="store" :size="22" /></span>
            <div class="s-info">
              <div class="faint small">{{ t('seller') }}</div>
              <b>{{ data.seller.name }}</b>
              <div class="s-stats">
                <span><SIcon name="star" :size="13" filled class="star" /> {{ data.seller.rating.toFixed(1) }} · {{ count(data.seller.reviewsCount, 'reviews') }}</span>
                <span>{{ count(data.seller.productsCount, 'items') }} · {{ count(data.seller.branchesCount, 'branches') }}</span>
                <span v-if="sinceYear">{{ t('sellerSince') }} {{ sinceYear }}</span>
              </div>
            </div>
            <button type="button" class="s-btn ghost chat-btn" @click="ask"><SIcon name="chat" :size="18" /> {{ t('askSeller') }}</button>
          </section>
        </div>
      </div>

      <section id="about" class="block">
        <h2>{{ t('aboutProduct') }}</h2>
        <div class="about">
          <div class="desc">
            <p v-for="(para, n) in paragraphs" :key="n">{{ para }}</p>
            <p v-if="!paragraphs.length" class="faint">—</p>
          </div>
          <dl class="specs">
            <div class="specs-title">{{ t('specs') }}</div>
            <template v-for="[k, v] in specs" :key="k"><dt>{{ k }}</dt><dd>{{ v }}</dd></template>
          </dl>
        </div>
      </section>

      <section id="reviews" class="block">
        <h2>{{ t('reviewsTitle') }} <span class="count">{{ card.reviewsCount || '' }}</span></h2>
        <ReviewsBlock :product-id="card.id" />
      </section>

      <section v-if="viewed.length" class="s-section">
        <div class="s-section-head"><h2>{{ t('viewed') }}</h2></div>
        <ProductRail :items="viewed" />
      </section>

      <section v-if="recommended.length" class="s-section">
        <div class="s-section-head"><h2>{{ t('recommended') }}</h2></div>
        <ProductRail :items="recommended" />
      </section>

      <!-- Phone: price + buy always within reach -->
      <div class="sticky-buy">
        <div class="sb-price">
          <strong :class="{ sale: current.oldPrice }">{{ price(current.price) }}</strong>
          <s v-if="current.oldPrice">{{ price(current.oldPrice) }}</s>
        </div>
        <BuyButton :id="card.id" :variant="variant" :max="card.maxQty" :in-stock="card.inStock" class="sb-btn" />
      </div>
    </template>

    <div v-else class="top loading">
      <div class="s-skel" style="aspect-ratio: 1" />
      <div><div class="s-skel" style="height: 34px; width: 70%" /><div class="s-skel" style="height: 180px; margin-top: 22px" /></div>
    </div>
  </div>
</template>

<style scoped>
.crumbs { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; font-size: 13.5px; color: var(--ink-3); padding: 18px 0 14px; }
.crumbs a:hover { color: var(--blue); }
.top { display: grid; grid-template-columns: minmax(0, 1.05fr) minmax(0, 1fr); gap: 36px; align-items: start; }
.top :deep(.stage) { box-shadow: var(--lift); }
.disc { position: absolute; left: 14px; top: 14px; background: var(--green); color: #fff; font-weight: 700; padding: 5px 11px; border-radius: 10px; font-size: 15px; }
.info { display: flex; flex-direction: column; gap: 16px; min-width: 0; }
h1 { font-size: 30px; line-height: 1.15; letter-spacing: -0.025em; margin: 0; }
.meta-row { display: flex; align-items: center; gap: 8px; margin-top: -6px; font-size: 14.5px; }
.rating { display: inline-flex; align-items: center; gap: 6px; border: 0; background: none; padding: 0; cursor: pointer; color: var(--ink-2); }
.rating:hover span { color: var(--blue); text-decoration: underline; }
.star { color: var(--amber); }
.faint { color: var(--ink-3); }
.small { font-size: 12.5px; }
.lead { margin: 0; color: var(--ink-2); line-height: 1.55; }
.link { border: 0; background: none; padding: 0; color: var(--blue); font-weight: 600; cursor: pointer; font-size: inherit; }
.link.small { font-size: 13px; margin-left: auto; }
.label { font-size: 13px; font-weight: 600; color: var(--ink-3); margin-bottom: 8px; }
.seg { display: flex; gap: 8px; flex-wrap: wrap; }
.seg button {
  display: flex; flex-direction: column; align-items: flex-start; gap: 1px; padding: 9px 14px; border-radius: 12px;
  border: 1.5px solid var(--line); background: var(--card); cursor: pointer; min-width: 96px; transition: border-color .15s;
}
.seg button:hover { border-color: var(--blue-100); }
.seg button.on { border-color: var(--blue); background: var(--blue-50); }
.seg small { color: var(--ink-3); font-size: 12.5px; }
.seg button.on small { color: var(--blue-600); }

.buybox { background: var(--card); border-radius: 20px; padding: 20px; box-shadow: var(--lift); display: flex; flex-direction: column; gap: 12px; }
.price-row { display: flex; align-items: baseline; gap: 10px; flex-wrap: wrap; }
.price-row strong { font-size: 32px; letter-spacing: -0.025em; }
.price-row strong.sale { color: var(--green); }
.price-row s { color: var(--ink-3); font-size: 16px; }
.save { font-size: 13px; font-weight: 650; color: var(--green); background: var(--green-50); padding: 3px 8px; border-radius: 8px; }
.stock { display: flex; align-items: center; gap: 7px; font-size: 14px; color: var(--green); font-weight: 550; }
.stock.low { color: #c77700; }
.stock.out { color: var(--red); }
.branches { list-style: none; margin: 0; padding: 4px 0; display: flex; flex-direction: column; gap: 2px; border-top: 1px solid var(--line); }
.branches li { display: flex; align-items: center; gap: 10px; padding: 8px 2px; font-size: 13.5px; }
.branches li > svg { color: var(--blue); flex: none; }
.branches span { display: flex; flex-direction: column; flex: 1; min-width: 0; }
.branches small { color: var(--ink-3); }
.branches em { font-style: normal; font-weight: 600; white-space: nowrap; }
.branches li.no { opacity: .55; }
.actions { display: flex; gap: 10px; align-items: center; }
.grow { flex: 1; }
.to-cart { align-self: center; color: var(--blue) !important; font-weight: 600; font-size: 14px; display: inline-flex; align-items: center; }

.seller { display: flex; align-items: center; gap: 14px; flex-wrap: wrap; padding: 16px 18px; border-radius: 18px; border: 1px solid var(--line); background: var(--card); }
.s-ava { width: 48px; height: 48px; border-radius: 14px; background: var(--blue); color: #fff; display: grid; place-items: center; flex: none; }
.s-info { flex: 1; min-width: 180px; }
.s-info b { font-size: 16px; }
.s-stats { display: flex; flex-direction: column; gap: 1px; font-size: 13px; color: var(--ink-2); margin-top: 3px; }
.s-stats svg { vertical-align: -1px; }
.chat-btn { height: 42px; }

.block { margin-top: 44px; }
.block h2 { font-size: 22px; letter-spacing: -0.015em; margin: 0 0 16px; }
.block h2 .count { color: var(--ink-3); font-weight: 500; font-size: 17px; }
.about { display: grid; grid-template-columns: 1.3fr 1fr; gap: 28px; background: var(--card); border-radius: 20px; padding: 24px; box-shadow: var(--lift); }
.desc p { margin: 0 0 12px; line-height: 1.65; color: var(--ink-2); }
.specs { display: grid; grid-template-columns: auto 1fr; gap: 10px 16px; margin: 0; align-content: start; font-size: 14.5px; }
.specs-title { grid-column: 1 / -1; font-weight: 700; margin-bottom: 2px; }
.specs dt { color: var(--ink-3); }
.specs dd { margin: 0; }
.sticky-buy { display: none; }
.loading { margin-top: 20px; }

@media (max-width: 900px) {
  .top { grid-template-columns: 1fr; gap: 20px; }
  .about { grid-template-columns: 1fr; padding: 18px; }
  h1 { font-size: 24px; }
}
@media (max-width: 640px) {
  .crumbs { padding-top: 12px; }
  .price-row strong { font-size: 27px; }
  .buybox { padding: 16px; }
  .sticky-buy {
    display: flex; align-items: center; gap: 12px; position: fixed; left: 0; right: 0; bottom: calc(64px + env(safe-area-inset-bottom));
    z-index: 84; background: rgb(255 255 255 / 97%); border-top: 1px solid var(--line); padding: 10px 14px; backdrop-filter: blur(10px);
  }
  .sb-price { display: flex; flex-direction: column; line-height: 1.2; }
  .sb-price strong { font-size: 18px; }
  .sb-price strong.sale { color: var(--green); }
  .sb-price s { font-size: 12.5px; color: var(--ink-3); }
  .sb-btn { flex: 1; }
  .s-wrap { padding-bottom: 70px; }
}
</style>
