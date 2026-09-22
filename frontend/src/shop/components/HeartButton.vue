<script setup lang="ts">
import { computed } from 'vue'
import { t } from '../i18n'
import { isFavorite, toggleFavorite } from '../state/favorites'
import { showToast } from '../state/toast'
import SIcon from './SIcon.vue'

// Heart toggle used on product cards and the product page. `label` shows the text next to the icon.
const props = defineProps<{ id: number; label?: boolean; size?: number }>()
const on = computed(() => isFavorite(props.id))
function toggle() {
  const added = toggleFavorite(props.id)
  showToast(t(added ? 'addedToFav' : 'removedFromFav'), added ? '/favorites' : undefined, added ? t('open') : undefined)
}
</script>

<template>
  <button type="button" class="heart" :class="{ on, labeled: label }" :aria-pressed="on"
    :aria-label="on ? t('inFav') : t('addToFav')" :title="on ? t('inFav') : t('addToFav')" @click.stop.prevent="toggle">
    <SIcon name="heart" :size="size ?? 20" :filled="on" />
    <span v-if="label">{{ on ? t('inFav') : t('addToFav') }}</span>
  </button>
</template>

<style scoped>
.heart {
  display: inline-grid; place-items: center; width: 38px; height: 38px; border-radius: 50%; border: 0; cursor: pointer;
  background: rgb(255 255 255 / 92%); color: var(--ink-2); box-shadow: 0 1px 4px rgb(21 32 51 / 12%); transition: color .15s, transform .15s;
}
.heart:hover { color: var(--red); }
.heart:active { transform: scale(.9); }
.heart.on { color: var(--red); }
.heart.on svg { animation: beat .35s ease-out; }
@keyframes beat { 40% { transform: scale(1.25); } }
.heart.labeled {
  display: inline-flex; gap: 8px; width: auto; height: 48px; padding: 0 16px; border-radius: 14px; box-shadow: none;
  background: var(--page); font-weight: 600; color: var(--ink);
}
.heart.labeled svg { color: var(--ink-2); }
.heart.labeled.on svg, .heart.labeled:hover svg { color: var(--red); }
</style>
