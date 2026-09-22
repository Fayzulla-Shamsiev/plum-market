<script setup lang="ts">
import { computed } from 'vue'
import type { Card } from '../api'
import { price, t, weight } from '../i18n'
import { defaultVariant } from '../state/cart'
import BuyButton from './BuyButton.vue'
import HeartButton from './HeartButton.vue'
import ProductImage from './ProductImage.vue'

// Catalog tile: photo, price (with old price and −%), name, rating, a heart and "Купить" / −qty+.
// The image and name link to the product page; the buttons act in place.
const props = defineProps<{ p: Card }>()
const to = computed(() => `/product/${props.p.id}`)
const meta = computed(() => weight(props.p.weightGrams))
</script>

<template>
  <article class="pcard" :class="{ off: !p.inStock }">
    <div class="media">
      <RouterLink :to="to" tabindex="-1" aria-hidden="true"><ProductImage :id="p.id" :name="p.name" :src="p.image" /></RouterLink>
      <span v-if="p.discountPercent" class="disc">−{{ p.discountPercent }}%</span>
      <HeartButton :id="p.id" class="fav" :size="18" />
      <span v-if="!p.inStock" class="stock">{{ t('outOfStock') }}</span>
    </div>
    <div class="body">
      <div class="prices">
        <strong class="now" :class="{ sale: p.oldPrice }">{{ price(p.price) }}</strong>
        <s v-if="p.oldPrice" class="old">{{ price(p.oldPrice) }}</s>
      </div>
      <RouterLink :to="to" class="name" :title="p.name">{{ p.name }}</RouterLink>
      <div class="foot">
        <span v-if="p.reviewsCount" class="rating">
          <svg width="14" height="14" viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="m12 2.5 2.9 6 6.6.9-4.8 4.6 1.2 6.5L12 17.4l-5.9 3.1 1.2-6.5L2.5 9.4l6.6-.9z" /></svg>
          {{ p.rating.toFixed(1) }}
          <span class="cnt">· {{ p.reviewsCount }}</span>
        </span>
        <span v-else class="cnt">{{ t('noReviews') }}</span>
        <span v-if="meta" class="meta">{{ meta }}</span>
      </div>
      <BuyButton :id="p.id" :variant="defaultVariant(p)" :max="p.maxQty" :in-stock="p.inStock" class="buy" />
    </div>
  </article>
</template>

<style scoped>
.pcard {
  display: flex; flex-direction: column; background: var(--card); border-radius: var(--r-lg); overflow: hidden;
  box-shadow: var(--lift); transition: transform .18s, box-shadow .18s; min-width: 0;
}
.pcard:hover { transform: translateY(-2px); box-shadow: 0 2px 4px rgb(21 32 51 / 6%), 0 12px 28px rgb(21 32 51 / 10%); }
.pcard:hover :deep(.ph) { transform: scale(1.06) rotate(-3deg); }
.media { position: relative; }
.media > a { display: block; }
.disc {
  position: absolute; left: 10px; top: 10px; background: var(--green); color: #fff; font-weight: 700; font-size: 13px;
  padding: 3px 8px; border-radius: 8px; pointer-events: none;
}
.fav { position: absolute; right: 8px; top: 8px; }
.stock {
  position: absolute; inset: auto 10px 10px; text-align: center; background: rgb(21 32 51 / 72%); color: #fff;
  font-size: 12.5px; font-weight: 600; padding: 5px; border-radius: 8px; backdrop-filter: blur(4px); pointer-events: none;
}
.off .media :deep(.pimg) { filter: grayscale(.7); opacity: .75; }
.body { padding: 12px 14px 14px; display: flex; flex-direction: column; gap: 4px; flex: 1; }
.prices { display: flex; align-items: baseline; flex-wrap: wrap; column-gap: 8px; }
.now { font-size: 17px; font-weight: 750; letter-spacing: -0.01em; }
.now.sale { color: var(--green); }
.old { color: var(--ink-3); font-size: 13px; }
.name {
  color: var(--ink); line-height: 1.35; font-size: 14.5px; display: -webkit-box; -webkit-line-clamp: 2; line-clamp: 2;
  -webkit-box-orient: vertical; overflow: hidden; min-height: calc(1.35em * 2);
}
.name:hover { color: var(--blue); }
.foot { margin-top: auto; padding-top: 6px; display: flex; align-items: center; gap: 8px; font-size: 13px; }
.rating { display: inline-flex; align-items: center; gap: 3px; color: var(--ink); font-weight: 600; }
.rating svg { color: var(--amber); }
.cnt { color: var(--ink-3); font-weight: 400; }
.meta { margin-left: auto; color: var(--ink-3); }
.buy { margin-top: 10px; }
@media (max-width: 640px) {
  .body { padding: 10px 11px 12px; }
  .now { font-size: 15.5px; }
  .name { font-size: 13.5px; }
  .meta { display: none; }
  .fav { width: 34px; height: 34px; }
}
</style>
