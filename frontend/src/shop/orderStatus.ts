import type { OrderStatus } from './api'
import { t, type Key } from './i18n'
import type { DeliveryType } from './api'

// Customer-facing order statuses, the same flow the store works through:
// Новый → В сборке → Готов к отправке → Передан в доставку → В пути → Доставлен → Завершён
// (pickup: Новый → В сборке → Готов к выдаче → Завершён).
export function statusKey(s: OrderStatus, d: DeliveryType): Key {
  if (s === 'Ready' && d === 'Pickup') return 'st_ReadyPickup'
  return `st_${s}` as Key
}
export const statusText = (s: OrderStatus, d: DeliveryType) => t(statusKey(s, d))

/** Fallback when an older API response has no steps. */
export function steps(d: DeliveryType): OrderStatus[] {
  return d === 'Pickup' ? ['New', 'Assembling', 'Ready', 'Completed'] : ['New', 'Assembling', 'Ready', 'HandedToCourier', 'OnTheWay', 'Delivered', 'Completed']
}
export const tone = (s: OrderStatus) =>
  s === 'Cancelled' ? 'grey' : s === 'Completed' || s === 'Delivered' ? 'green' : s === 'New' || s === 'Assembling' ? 'amber' : 'blue'
