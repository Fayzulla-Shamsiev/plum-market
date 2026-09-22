<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { shopApi, type MyReviews, type ReviewProduct } from '../api'
import BackLink from '../components/BackLink.vue'
import ProductImage from '../components/ProductImage.vue'
import SModal from '../components/SModal.vue'
import { lang, longDate, t } from '../i18n'
import { requireLogin, signedIn } from '../state/auth'
import { persisted } from '../state/persist'
import { showToast } from '../state/toast'

// "Мои отзывы": products from received orders waiting for a rating, and reviews already written (with the store's reply).
const tab = persisted<'pending' | 'rated'>('plum.shop.reviewsTab', 'pending')
const data = ref<MyReviews | null>(null)
async function load() {
  if (!signedIn.value) return
  data.value = await shopApi.myReviews().catch(() => data.value)
}
onMounted(() => requireLogin(load))
watch([signedIn, lang], load)

// ---- Write / edit dialog ----
const editing = ref<{ product: ReviewProduct; reviewId: number | null } | null>(null)
const rating = ref(0)
const hover = ref(0)
const comment = ref('')
const error = ref('')
const busy = ref(false)
function write(product: ReviewProduct) {
  editing.value = { product, reviewId: null }
  rating.value = 0
  comment.value = ''
  error.value = ''
}
function edit(r: MyReviews['rated'][number]) {
  editing.value = { product: r.product, reviewId: r.id }
  rating.value = r.rating
  comment.value = r.comment
  error.value = ''
}
async function submit() {
  const e = editing.value
  if (!e) return
  if (!rating.value) { error.value = t('yourRating'); return }
  busy.value = true
  try {
    if (e.reviewId) await shopApi.updateReview(e.reviewId, e.product.id, rating.value, comment.value.trim())
    else await shopApi.createReview(e.product.id, rating.value, comment.value.trim())
    editing.value = null
    showToast(t('thanksReview'))
    tab.value = 'rated'
    await load()
  } catch (err) {
    error.value = (err as Error).message
  } finally {
    busy.value = false
  }
}
const labels = ['', '😞', '😕', '😐', '🙂', '😍']
</script>

<template>
  <div class="s-wrap narrow">
    <BackLink to="/profile" :label="t('profile')" />
    <h1>{{ t('myReviews') }}</h1>

    <template v-if="signedIn">
      <div class="tabs" role="tablist">
        <button type="button" role="tab" :aria-selected="tab === 'pending'" :class="{ on: tab === 'pending' }" @click="tab = 'pending'">
          {{ t('awaiting') }} <small v-if="data">{{ data.pending.length }}</small>
        </button>
        <button type="button" role="tab" :aria-selected="tab === 'rated'" :class="{ on: tab === 'rated' }" @click="tab = 'rated'">
          {{ t('rated') }} <small v-if="data">{{ data.rated.length }}</small>
        </button>
      </div>

      <template v-if="data && tab === 'pending'">
        <p v-if="data.pending.length" class="hint">{{ t('pendingHint') }}</p>
        <div v-else class="s-empty"><div class="big">⭐</div><h3>{{ t('noPending') }}</h3></div>
        <div class="list">
          <article v-for="p in data.pending" :key="p.product.id" class="row">
            <RouterLink :to="`/product/${p.product.id}`" class="th"><ProductImage :id="p.product.id" :name="p.product.name" :src="p.product.image" /></RouterLink>
            <div class="grow">
              <RouterLink :to="`/product/${p.product.id}`" class="nm">{{ p.product.name }}</RouterLink>
              <div class="faint small">{{ t('receivedOn') }} {{ longDate(p.receivedAt) }} · <RouterLink :to="`/profile/orders/${p.orderId}`">{{ t('order') }} №{{ p.orderId }}</RouterLink></div>
            </div>
            <button type="button" class="s-btn rate" @click="write(p.product)">{{ t('rate') }}</button>
          </article>
        </div>
      </template>

      <template v-if="data && tab === 'rated'">
        <div v-if="!data.rated.length" class="s-empty"><div class="big">📝</div><h3>{{ t('noRated') }}</h3></div>
        <div class="list">
          <article v-for="r in data.rated" :key="r.id" class="review">
            <div class="head">
              <RouterLink :to="`/product/${r.product.id}`" class="th"><ProductImage :id="r.product.id" :name="r.product.name" :src="r.product.image" /></RouterLink>
              <div class="grow">
                <RouterLink :to="`/product/${r.product.id}`" class="nm">{{ r.product.name }}</RouterLink>
                <div class="stars" :aria-label="`${r.rating} / 5`"><span v-for="n in 5" :key="n" :class="{ lit: n <= r.rating }">★</span></div>
              </div>
              <span class="faint small">{{ longDate(r.createdAt) }}</span>
            </div>
            <p v-if="r.comment" class="text">{{ r.comment }}</p>
            <div v-if="r.reply" class="reply"><b>{{ t('storeReply') }}</b><p>{{ r.reply }}</p></div>
            <button type="button" class="link" @click="edit(r)">{{ t('editReview') }}</button>
          </article>
        </div>
      </template>
    </template>

    <SModal v-if="editing" :title="editing.reviewId ? t('editReview') : t('rate')" @close="editing = null">
      <div class="dlg-product">
        <span class="th sm"><ProductImage :id="editing.product.id" :name="editing.product.name" :src="editing.product.image" /></span>
        <b>{{ editing.product.name }}</b>
      </div>
      <div class="label">{{ t('yourRating') }}</div>
      <div class="picker" role="radiogroup" :aria-label="t('yourRating')" @mouseleave="hover = 0">
        <button v-for="n in 5" :key="n" type="button" role="radio" :aria-checked="rating === n" :aria-label="`${n}`"
          :class="{ lit: n <= (hover || rating) }" @mouseenter="hover = n" @click="rating = n">★</button>
        <span class="mood">{{ labels[hover || rating] }}</span>
      </div>
      <textarea v-model="comment" class="s-input" rows="4" maxlength="1000" :placeholder="t('yourReview')" />
      <p v-if="error" class="err">{{ error }}</p>
      <template #footer>
        <button type="button" class="s-btn ghost" @click="editing = null">{{ t('cancel') }}</button>
        <button type="button" class="s-btn" :disabled="busy || !rating" @click="submit">{{ t('send2') }}</button>
      </template>
    </SModal>
  </div>
</template>

<style scoped>
.narrow { max-width: 820px; }
h1 { font-size: 30px; letter-spacing: -0.025em; margin: 0 0 16px; }
.tabs { display: inline-flex; background: var(--card); border-radius: 14px; padding: 4px; box-shadow: var(--lift); margin-bottom: 14px; }
.tabs button { border: 0; background: none; padding: 9px 16px; border-radius: 10px; font-weight: 600; cursor: pointer; color: var(--ink-2); }
.tabs button.on { background: var(--ink); color: #fff; }
.tabs small { opacity: .6; margin-left: 4px; }
.hint { color: var(--ink-3); margin: 0 0 10px; font-size: 14px; }
.list { display: flex; flex-direction: column; gap: 10px; }
.row, .review { background: var(--card); border-radius: 18px; padding: 14px; box-shadow: var(--lift); }
.row { display: flex; align-items: center; gap: 14px; }
.th { width: 60px; height: 60px; border-radius: 14px; overflow: hidden; flex: none; display: block; }
.th.sm { width: 44px; height: 44px; border-radius: 10px; }
.th :deep(.ph) { font-size: 30px; }
.grow { flex: 1; min-width: 0; }
.nm { font-weight: 600; }
.nm:hover { color: var(--blue); }
.faint { color: var(--ink-3); }
.small { font-size: 13px; }
.small a { color: var(--blue); }
.rate { height: 40px; padding: 0 16px; }
.review { display: flex; flex-direction: column; gap: 8px; }
.head { display: flex; align-items: center; gap: 14px; }
.stars { color: #d8dde6; letter-spacing: 1px; }
.stars .lit { color: var(--amber); }
.text { margin: 0; line-height: 1.55; }
.reply { padding: 10px 14px; background: var(--blue-50); border-radius: 4px 14px 14px 14px; font-size: 14px; }
.reply b { color: var(--blue-600); font-size: 13px; }
.reply p { margin: 4px 0 0; color: var(--ink-2); }
.link { align-self: flex-start; border: 0; background: none; color: var(--blue); font-weight: 600; cursor: pointer; padding: 0; }
.dlg-product { display: flex; align-items: center; gap: 12px; margin-bottom: 14px; }
.label { font-size: 14px; font-weight: 550; color: var(--ink-2); }
.picker { display: flex; align-items: center; gap: 4px; margin: 4px 0 12px; }
.picker button { border: 0; background: none; font-size: 36px; line-height: 1; color: #d8dde6; cursor: pointer; padding: 0 2px; transition: transform .1s, color .1s; }
.picker button.lit { color: var(--amber); }
.picker button:hover { transform: scale(1.12); }
.mood { font-size: 26px; margin-left: 8px; }
.err { color: var(--red); font-size: 13px; }
@media (max-width: 640px) {
  h1 { font-size: 24px; }
  .row { flex-wrap: wrap; }
  .rate { width: 100%; }
}
</style>
