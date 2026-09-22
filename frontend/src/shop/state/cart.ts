import { computed } from 'vue'
import type { Card } from '../api'
import { persisted } from './persist'

// The cart lives in the browser until checkout (next iteration); prices are always re-quoted by the server.
/** `off` = unticked in the cart: stays in the cart but isn't ordered. */
export interface CartLine { id: number; variant: string | null; qty: number; off?: boolean }

export const cartLines = persisted<CartLine[]>('plum.shop.cart', [])
export const cartCount = computed(() => cartLines.value.reduce((s, l) => s + l.qty, 0))

const same = (l: CartLine, id: number, variant: string | null) => l.id === id && l.variant === variant

/** Default variant of a card = the first one (what the catalog shows the price for). */
export const defaultVariant = (c: Pick<Card, 'variants'>) => c.variants[0] ?? null

export function qtyIn(id: number, variant: string | null) {
  return cartLines.value.find(l => same(l, id, variant))?.qty ?? 0
}

/** Sets the quantity of one line; 0 removes it. `max` (stock) caps it; null = no limit. */
export function setQty(id: number, variant: string | null, qty: number, max: number | null = null) {
  const q = Math.max(0, Math.min(qty, max ?? 999))
  const i = cartLines.value.findIndex(l => same(l, id, variant))
  if (q === 0) {
    if (i >= 0) cartLines.value.splice(i, 1)
  } else if (i >= 0) {
    cartLines.value[i]!.qty = q
  } else {
    cartLines.value.push({ id, variant, qty: q })
  }
}

export function removeLines(keys: { id: number; variant: string | null }[]) {
  cartLines.value = cartLines.value.filter(l => !keys.some(k => same(l, k.id, k.variant)))
}

export const selectedLines = computed(() => cartLines.value.filter(l => !l.off))
export const allSelected = computed(() => cartLines.value.length > 0 && cartLines.value.every(l => !l.off))

export function toggleSelected(id: number, variant: string | null) {
  const line = cartLines.value.find(l => same(l, id, variant))
  if (line) line.off = !line.off
}
export function selectAll(on: boolean) {
  for (const l of cartLines.value) l.off = !on
}
export function removeSelected() {
  cartLines.value = cartLines.value.filter(l => l.off)
}
