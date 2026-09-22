<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { shopApi, type CatalogPage, type Card, type Sort } from '../api'
import ProductCard from '../components/ProductCard.vue'
import SIcon from '../components/SIcon.vue'
import { count, lang, t, type Key } from '../i18n'
import { useMeta } from '../store'

const route = useRoute()
const router = useRouter()
const meta = useMeta()

const categoryId = computed(() => (route.params.id ? Number(route.params.id) : undefined))
const sort = computed(() => (route.query.sort as Sort | undefined) ?? undefined)
const inStockOnly = computed(() => route.query.stock === '1')

const data = ref<CatalogPage | null>(null)
const items = ref<Card[]>([])
const loading = ref(false)
const failed = ref(false)

async function load(page = 1) {
  loading.value = true
  failed.value = false
  try {
    const res = await shopApi.catalog({ categoryId: categoryId.value, sort: sort.value, inStockOnly: inStockOnly.value || undefined, page })
    data.value = res
    items.value = page === 1 ? res.items : [...items.value, ...res.items]
  } catch {
    failed.value = true
  } finally {
    loading.value = false
  }
}
watch([categoryId, sort, inStockOnly, lang], () => load(1), { immediate: true })
watch(categoryId, () => window.scrollTo({ top: 0 }))

const sorts: [Sort, Key][] = [
  ['manual', 'sortManual'], ['popular', 'sortPopular'], ['price_asc', 'sortPriceAsc'],
  ['price_desc', 'sortPriceDesc'], ['new', 'sortNew'], ['rating', 'sortRating'],
]
function setQuery(patch: Record<string, string | undefined>) {
  router.replace({ query: { ...route.query, ...patch } })
}

const cat = computed(() => data.value?.category ?? null)
// Sub-category chips: a parent shows its children; a leaf shows its siblings so you can hop sideways.
const chips = computed(() => {
  const c = cat.value
  if (!c) return { all: null as number | null, list: meta.value?.categories ?? [], current: undefined as number | undefined }
  if (c.children.length) return { all: c.id, list: c.children, current: undefined }
  return { all: c.parentId, list: c.siblings, current: c.id }
})
const hasMore = computed(() => !!data.value && items.value.length < data.value.total)
</script>

<template>
  <div class="s-wrap">
    <nav class="crumbs" aria-label="breadcrumbs">
      <RouterLink to="/">{{ t('home') }}</RouterLink>
      <SIcon name="chevronRight" :size="14" />
      <template v-if="cat">
        <RouterLink to="/catalog">{{ t('catalog') }}</RouterLink>
        <template v-for="(c, i) in cat.trail" :key="c.id">
          <SIcon name="chevronRight" :size="14" />
          <RouterLink v-if="i < cat.trail.length - 1" :to="`/catalog/${c.id}`">{{ c.name }}</RouterLink>
          <span v-else aria-current="page">{{ c.name }}</span>
        </template>
      </template>
      <span v-else aria-current="page">{{ t('catalog') }}</span>
    </nav>

    <header class="head">
      <h1>{{ cat?.name ?? t('catalog') }}</h1>
      <span v-if="data" class="total">{{ count(data.total, 'items') }}</span>
    </header>
    <p v-if="cat?.description" class="desc">{{ cat.description }}</p>
    <img v-if="cat?.bannerUrl" :src="cat.bannerUrl" alt="" class="banner" />

    <div v-if="chips.list.length" class="chips s-scroll">
      <RouterLink :to="chips.all ? `/catalog/${chips.all}` : '/catalog'" class="s-chip" :class="{ active: chips.current === undefined }">
        {{ t('all') }}
      </RouterLink>
      <RouterLink v-for="c in chips.list" :key="c.id" :to="`/catalog/${c.id}`" class="s-chip" :class="{ active: chips.current === c.id }">
        {{ c.name }} <small>{{ c.productsCount }}</small>
      </RouterLink>
    </div>

    <div class="tools">
      <label class="toggle">
        <input type="checkbox" :checked="inStockOnly" @change="setQuery({ stock: ($event.target as HTMLInputElement).checked ? '1' : undefined })" />
        <span class="sw" aria-hidden="true" />
        {{ t('inStockOnly') }}
      </label>
      <label class="sort">
        <span class="sr">{{ t('sort') }}</span>
        <select class="s-select" :value="data?.sort ?? 'manual'" @change="setQuery({ sort: ($event.target as HTMLSelectElement).value })">
          <option v-for="[v, k] in sorts" :key="v" :value="v">{{ t(k) }}</option>
        </select>
      </label>
    </div>

    <div v-if="failed" class="s-empty">
      <h3>{{ t('error') }}</h3>
      <button class="s-btn ghost" @click="load(1)">{{ t('retry') }}</button>
    </div>
    <div v-else-if="data && !items.length" class="s-empty">
      <div class="big">🧺</div>
      <h3>{{ t('emptyCategory') }}</h3>
    </div>
    <div v-else class="s-grid" :class="{ dim: loading && items.length }">
      <ProductCard v-for="p in items" :key="p.id" :p="p" />
      <template v-if="!data"><div v-for="i in 10" :key="i" class="s-skel" style="height: 320px" /></template>
    </div>

    <div v-if="hasMore" class="more-wrap">
      <button class="s-btn ghost" :disabled="loading" @click="load((data?.page ?? 1) + 1)">
        {{ t('showMore') }} · {{ (data?.total ?? 0) - items.length }}
      </button>
    </div>
  </div>
</template>

<style scoped>
.crumbs { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; font-size: 13.5px; color: var(--ink-3); padding: 18px 0 6px; }
.crumbs a:hover { color: var(--blue); }
.crumbs span[aria-current] { color: var(--ink-2); }
.head { display: flex; align-items: baseline; gap: 12px; flex-wrap: wrap; }
h1 { font-size: 30px; letter-spacing: -0.025em; margin: 0; }
.total { color: var(--ink-3); }
.desc { color: var(--ink-2); margin: 6px 0 0; max-width: 720px; }
.banner { width: 100%; border-radius: 20px; margin-top: 16px; aspect-ratio: 4 / 1; object-fit: cover; }
.chips { margin-top: 18px; }
.tools { display: flex; align-items: center; gap: 16px; margin: 18px 0 16px; }
.toggle { display: inline-flex; align-items: center; gap: 10px; cursor: pointer; font-size: 14px; color: var(--ink-2); user-select: none; }
.toggle input { position: absolute; opacity: 0; pointer-events: none; }
.sw { width: 38px; height: 22px; border-radius: 11px; background: #d5dbe5; position: relative; transition: background .15s; }
.sw::after { content: ''; position: absolute; left: 3px; top: 3px; width: 16px; height: 16px; border-radius: 50%; background: #fff; box-shadow: 0 1px 2px rgb(0 0 0 / 20%); transition: transform .15s; }
.toggle input:checked + .sw { background: var(--green); }
.toggle input:checked + .sw::after { transform: translateX(16px); }
.toggle input:focus-visible + .sw { outline: 2px solid var(--blue); outline-offset: 2px; }
.sort { margin-left: auto; }
.sr { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); }
.dim { opacity: .6; transition: opacity .15s; }
.more-wrap { display: flex; justify-content: center; margin-top: 28px; }
@media (max-width: 640px) {
  h1 { font-size: 24px; }
  .crumbs { padding-top: 12px; }
  .tools { margin: 14px 0 12px; }
}
</style>
