<script setup lang="ts">
import { ref, watch } from 'vue'
import { shopApi, type Card } from '../api'
import ProductCard from '../components/ProductCard.vue'
import { count, lang, t } from '../i18n'
import { favoriteIds } from '../state/favorites'

// Saved products. Un-hearting one removes it from this page right away.
const cards = ref<Card[]>([])
const loaded = ref(false)
watch([() => favoriteIds.value.join(','), lang], async () => {
  cards.value = await shopApi.cards(favoriteIds.value).catch(() => cards.value)
  loaded.value = true
}, { immediate: true })
</script>

<template>
  <div class="s-wrap">
    <header class="head">
      <h1>{{ t('favorites') }}</h1>
      <span v-if="cards.length" class="count">{{ count(cards.length, 'items') }}</span>
    </header>
    <div v-if="loaded && !favoriteIds.length" class="s-empty">
      <div class="big">🤍</div>
      <h3>{{ t('favEmpty') }}</h3>
      <p>{{ t('favEmptyHint') }}</p>
      <RouterLink to="/catalog" class="s-btn">{{ t('toCatalog') }}</RouterLink>
    </div>
    <TransitionGroup v-else tag="div" name="fade" class="s-grid">
      <ProductCard v-for="p in cards.filter(c => favoriteIds.includes(c.id))" :key="p.id" :p="p" />
    </TransitionGroup>
  </div>
</template>

<style scoped>
.head { display: flex; align-items: baseline; gap: 12px; padding: 22px 0 16px; }
h1 { font-size: 30px; letter-spacing: -0.025em; margin: 0; }
.count { color: var(--ink-3); }
.s-empty p { margin: 0 0 18px; }
.fade-leave-active { transition: opacity .2s, transform .2s; }
.fade-leave-to { opacity: 0; transform: scale(.96); }
@media (max-width: 640px) { h1 { font-size: 24px; } }
</style>
