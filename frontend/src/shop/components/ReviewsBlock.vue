<script setup lang="ts">
import { ref, watch } from 'vue'
import { shopApi, type ReviewsPage } from '../api'
import { count, lang, longDate, t } from '../i18n'

// Product reviews: average + star breakdown (click a bar to filter), sorting, paged list with the store's replies.
const props = defineProps<{ productId: number }>()
const data = ref<ReviewsPage | null>(null)
const items = ref<ReviewsPage['items']>([])
const sort = ref('new')
const rating = ref<number | undefined>()
const loading = ref(false)

async function load(page = 1) {
  loading.value = true
  try {
    const res = await shopApi.reviews(props.productId, { sort: sort.value, rating: rating.value, page })
    data.value = res
    items.value = page === 1 ? res.items : [...items.value, ...res.items]
  } finally {
    loading.value = false
  }
}
watch(() => props.productId, () => { rating.value = undefined; sort.value = 'new' })
watch([() => props.productId, sort, rating, lang], () => load(1), { immediate: true })

const pct = (n: number) => (data.value?.count ? (n / data.value.count) * 100 : 0)
const initials = (name: string) => name.split(' ').map(x => x[0]).join('').slice(0, 2)
</script>

<template>
  <div v-if="data" class="reviews">
    <div v-if="!data.count" class="none">{{ t('noReviewsYet') }}</div>
    <template v-else>
      <aside class="summary">
        <div class="avg">{{ data.average.toFixed(1) }}</div>
        <div class="stars" :style="{ '--v': data.average / 5 }" aria-hidden="true">★★★★★</div>
        <div class="faint">{{ count(data.count, 'reviews') }}</div>
        <div class="bars">
          <button v-for="b in data.breakdown" :key="b.star" type="button" class="bar" :class="{ on: rating === b.star, empty: !b.count }"
            :disabled="!b.count" @click="rating = rating === b.star ? undefined : b.star">
            <span class="lbl">{{ b.star }} ★</span>
            <span class="track"><span :style="{ width: pct(b.count) + '%' }" /></span>
            <span class="n">{{ b.count }}</span>
          </button>
        </div>
      </aside>

      <div class="list">
        <div class="tools">
          <button v-if="rating" type="button" class="s-chip active" @click="rating = undefined">{{ rating }} ★ ✕</button>
          <select v-model="sort" class="s-select" :aria-label="t('sort')">
            <option value="new">{{ t('sortNewest') }}</option>
            <option value="high">{{ t('sortHigh') }}</option>
            <option value="low">{{ t('sortLow') }}</option>
          </select>
        </div>
        <article v-for="r in items" :key="r.id" class="review">
          <header>
            <span class="ava">{{ initials(r.author) }}</span>
            <div>
              <b>{{ r.author }}</b>
              <div class="faint small">{{ longDate(r.createdAt) }}</div>
            </div>
            <span class="r-stars" :aria-label="`${r.rating} / 5`">
              <span v-for="n in 5" :key="n" :class="{ lit: n <= r.rating }">★</span>
            </span>
          </header>
          <p>{{ r.comment }}</p>
          <div v-if="r.reply" class="reply">
            <b>{{ t('storeReply') }}</b>
            <p>{{ r.reply }}</p>
          </div>
        </article>
        <button v-if="items.length < data.total" type="button" class="s-btn ghost more" :disabled="loading" @click="load(data.page + 1)">
          {{ t('moreReviews') }} · {{ data.total - items.length }}
        </button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.reviews { display: grid; grid-template-columns: 280px 1fr; gap: 28px; align-items: start; }
.none { grid-column: 1 / -1; color: var(--ink-2); padding: 24px; background: var(--page); border-radius: 16px; text-align: center; }
.summary { position: sticky; top: 150px; background: var(--page); border-radius: 18px; padding: 20px; }
.avg { font-size: 44px; font-weight: 750; letter-spacing: -0.03em; line-height: 1; }
.stars {
  font-size: 22px; letter-spacing: 2px; margin: 6px 0 2px; background: linear-gradient(90deg, var(--amber) calc(var(--v) * 100%), #d8dde6 0);
  -webkit-background-clip: text; background-clip: text; color: transparent; display: inline-block;
}
.faint { color: var(--ink-3); font-size: 14px; }
.small { font-size: 12.5px; }
.bars { display: flex; flex-direction: column; gap: 4px; margin-top: 14px; }
.bar { display: grid; grid-template-columns: 36px 1fr 28px; align-items: center; gap: 8px; border: 0; background: none; padding: 4px 6px; border-radius: 8px; cursor: pointer; font-size: 13px; color: var(--ink-2); }
.bar:hover:not(:disabled), .bar.on { background: var(--card); }
.bar:disabled { cursor: default; opacity: .5; }
.track { height: 8px; border-radius: 4px; background: #dfe4ec; overflow: hidden; }
.track span { display: block; height: 100%; background: var(--amber); border-radius: 4px; }
.n { text-align: right; font-variant-numeric: tabular-nums; }
.tools { display: flex; justify-content: flex-end; gap: 10px; margin-bottom: 8px; }
.review { padding: 16px 0; border-bottom: 1px solid var(--line); }
.review header { display: flex; align-items: center; gap: 12px; }
.ava { width: 40px; height: 40px; border-radius: 50%; background: var(--blue-50); color: var(--blue); display: grid; place-items: center; font-weight: 700; font-size: 14px; flex: none; }
.r-stars { margin-left: auto; color: #d8dde6; letter-spacing: 1px; }
.r-stars .lit { color: var(--amber); }
.review > p { margin: 10px 0 0; line-height: 1.55; }
.reply { margin: 12px 0 0 52px; padding: 12px 14px; background: var(--blue-50); border-radius: 4px 14px 14px 14px; font-size: 14px; }
.reply b { color: var(--blue-600); font-size: 13px; }
.reply p { margin: 4px 0 0; color: var(--ink-2); }
.more { margin-top: 16px; }
@media (max-width: 760px) {
  .reviews { grid-template-columns: 1fr; gap: 16px; }
  .summary { position: static; }
  .reply { margin-left: 0; }
}
</style>
