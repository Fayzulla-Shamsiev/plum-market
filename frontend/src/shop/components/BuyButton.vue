<script setup lang="ts">
import { computed } from 'vue'
import { t } from '../i18n'
import { qtyIn, setQty } from '../state/cart'
import { showToast } from '../state/toast'
import SIcon from './SIcon.vue'

// "Купить" → becomes a −/+ stepper once the item is in the cart (spec: quantity changes right on the card).
const props = defineProps<{ id: number; variant: string | null; max: number | null; inStock: boolean; size?: 'sm' | 'lg' }>()
const qty = computed(() => qtyIn(props.id, props.variant))
const atMax = computed(() => props.max !== null && qty.value >= props.max)

function add() {
  setQty(props.id, props.variant, 1, props.max)
  showToast(t('addedToCart'), '/cart', t('goToCart'))
}
function inc() {
  if (atMax.value) return showToast(t('maxReached'))
  setQty(props.id, props.variant, qty.value + 1, props.max)
}
const dec = () => setQty(props.id, props.variant, qty.value - 1, props.max)
</script>

<template>
  <div class="buy" :class="size ?? 'sm'" @click.stop.prevent>
    <button v-if="!inStock" type="button" class="btn off" disabled>{{ t('outOfStock') }}</button>
    <button v-else-if="!qty" type="button" class="btn" @click="add">
      <SIcon name="cart" :size="size === 'lg' ? 20 : 17" /> {{ size === 'lg' ? t('toCart') : t('buy') }}
    </button>
    <div v-else class="stepper" role="group" :aria-label="t('inCart')">
      <button type="button" :aria-label="'−1'" @click="dec"><SIcon :name="qty === 1 ? 'trash' : 'minus'" :size="17" /></button>
      <output aria-live="polite">{{ qty }}</output>
      <button type="button" :aria-label="'+1'" :class="{ dim: atMax }" @click="inc"><SIcon name="plus" :size="17" /></button>
    </div>
  </div>
</template>

<style scoped>
.buy { width: 100%; }
.btn {
  width: 100%; height: 38px; border: 0; border-radius: 11px; background: var(--blue-50); color: var(--blue); font-weight: 650;
  display: inline-flex; align-items: center; justify-content: center; gap: 7px; cursor: pointer; transition: background .15s, color .15s;
}
.btn:hover { background: var(--blue); color: #fff; }
.btn.off { background: var(--page); color: var(--ink-3); cursor: default; }
.stepper {
  height: 38px; display: grid; grid-template-columns: 40px 1fr 40px; align-items: center; border-radius: 11px;
  background: var(--blue); color: #fff; overflow: hidden;
}
.stepper button { height: 100%; border: 0; background: none; color: inherit; cursor: pointer; display: grid; place-items: center; }
.stepper button:hover { background: rgb(255 255 255 / 14%); }
.stepper button.dim { opacity: .45; }
.stepper output { text-align: center; font-weight: 700; font-variant-numeric: tabular-nums; }
.lg .btn, .lg .stepper { height: 52px; border-radius: 14px; font-size: 16px; }
.lg .btn { background: var(--blue); color: #fff; }
.lg .btn:hover { background: var(--blue-600); }
.lg .btn.off { background: var(--page); color: var(--ink-3); }
.lg .stepper { grid-template-columns: 56px 1fr 56px; }
</style>
