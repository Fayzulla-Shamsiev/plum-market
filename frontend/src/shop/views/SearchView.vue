<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { shopApi, type Card, type SearchPage, type Sort } from '../api'
import ProductCard from '../components/ProductCard.vue'
import { count, lang, t, type Key } from '../i18n'
import { useMeta } from '../store'
import { emojiFor } from '../visuals'

const route = useRoute()
const router = useRouter()
const meta = useMeta()

const q = computed(() => (typeof route.query.q === 'string' ? route.query.q.trim() : ''))
const categoryId = computed(() => (route.query.cat ? Number(route.query.cat) : undefined))
const sort = computed(() => (route.query.sort as Sort | undefined) ?? 'relevance')

const data = ref<SearchPage | null>(null)
const items = ref<Card[]>([])
const loading = ref(false)
const failed = ref(false)

async function load(page = 1) {
  if (!q.value) { data.value = null; items.value = []; return }
  loading.value = true
  failed.value = false
  try {
    const res = await shopApi.search({ q: q.value, categoryId: categoryId.value, sort: sort.value, page })
    data.value = res
    items.value = page === 1 ? res.items : [...items.value, ...res.items]
  } catch {
    failed.value = true
  } finally {
    loading.value = false
  }
}
watch([q, categoryId, sort, lang], () => load(1), { immediate: true })

const sorts: [Sort, Key][] = [
  ['relevance', 'sortRelevance'], ['popular', 'sortPopular'], ['price_asc', 'sortPriceAsc'],
  ['price_desc', 'sortPriceDesc'], ['new', 'sortNew'], ['rating', 'sortRating'],
]
function setQuery(patch: Record<string, string | undefined>) {
  router.replace({ query: { ...route.query, ...patch } })
}
const facetTotal = computed(() => data.value?.facets.reduce((s, f) => s + f.count, 0) ?? 0)
const hasMore = computed(() => !!data.value && items.value.length < data.value.total)
</script>

<template>
  <div class="s-wrap">
    <header class="head">
      <div>
        <div class="eyebrow">{{ t('resultsFor') }}</div>
        <h1>«{{ q }}»</h1>
      </div>
      <span v-if="data" class="total">{{ count(data.total, 'items') }}</span>
    </header>

    <!-- Matching categories -->
    <div v-if="data?.categories.length" class="cats">
      <RouterLink v-for="c in data.categories" :key="c.id" :to="`/catalog/${c.id}`" class="cat">
        <span class="cat-ico">{{ emojiFor(c.name) }}</span>
        <span><b>{{ c.name }}</b><small>{{ c.path }}</small></span>
      </RouterLink>
    </div>

    <template v-if="data && data.total + facetTotal > 0">
      <div class="tools">
        <div v-if="data.facets.length > 1" class="s-scroll facets">
          <button class="s-chip" :class="{ active: !categoryId }" @click="setQuery({ cat: undefined })">
            {{ t('all') }} <small>{{ facetTotal }}</small>
          </button>
          <button v-for="f in data.facets" :key="f.id" class="s-chip" :class="{ active: categoryId === f.id }" @click="setQuery({ cat: String(f.id) })">
            {{ f.name }} <small>{{ f.count }}</small>
          </button>
        </div>
        <select class="s-select sort" :value="sort" :aria-label="t('sort')" @change="setQuery({ sort: ($event.target as HTMLSelectElement).value })">
          <option v-for="[v, k] in sorts" :key="v" :value="v">{{ t(k) }}</option>
        </select>
      </div>
    </template>

    <div v-if="failed" class="s-empty">
      <h3>{{ t('error') }}</h3>
      <button class="s-btn ghost" @click="load(1)">{{ t('retry') }}</button>
    </div>
    <div v-else-if="!q || (data && !items.length)" class="s-empty">
      <div class="big">🔍</div>
      <h3>{{ q ? t('nothingFound') : t('typeToSearch') }}</h3>
      <p v-if="q">{{ t('nothingFoundHint') }}</p>
      <div class="try s-scroll">
        <RouterLink v-for="c in meta?.categories ?? []" :key="c.id" :to="`/catalog/${c.id}`" class="s-chip">
          {{ emojiFor(c.name) }} {{ c.name }}
        </RouterLink>
      </div>
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
.head { display: flex; align-items: flex-end; gap: 12px; flex-wrap: wrap; padding-top: 22px; }
.eyebrow { color: var(--ink-3); font-size: 14px; }
h1 { font-size: 30px; letter-spacing: -0.025em; margin: 2px 0 0; word-break: break-word; }
.total { color: var(--ink-3); padding-bottom: 5px; }
.cats { display: flex; gap: 10px; flex-wrap: wrap; margin-top: 18px; }
.cat { display: flex; align-items: center; gap: 10px; padding: 8px 16px 8px 8px; border-radius: 14px; background: var(--card); box-shadow: var(--lift); transition: transform .15s; }
.cat:hover { transform: translateY(-1px); }
.cat > span:last-child { display: flex; flex-direction: column; line-height: 1.25; }
.cat small { color: var(--ink-3); font-size: 12.5px; }
.cat-ico { width: 40px; height: 40px; border-radius: 11px; background: var(--blue-50); display: grid; place-items: center; font-size: 21px; }
.tools { display: flex; align-items: center; gap: 12px; margin: 20px 0 16px; }
.facets { flex: 1; min-width: 0; }
.sort { margin-left: auto; flex: none; }
.try { justify-content: center; flex-wrap: wrap; margin-top: 18px; }
.s-empty p { margin: 0; }
.dim { opacity: .6; transition: opacity .15s; }
.more-wrap { display: flex; justify-content: center; margin-top: 28px; }
@media (max-width: 640px) {
  h1 { font-size: 24px; }
  .head { padding-top: 14px; }
  .tools { flex-direction: column; align-items: stretch; }
  .sort { margin-left: 0; }
}
</style>
