import type { DeliveryType, OrderStatus } from './api'
import { t, type Key } from './i18n'

// Customer-facing order statuses. "Просрочен" is an internal flag for the merchant; the customer still sees "Новый".
export function statusKey(s: OrderStatus, d: DeliveryType): Key {
  switch (s) {
    case 'New': case 'Overdue': return 'st_New'
    case 'InProgress': return 'st_InProgress'
    case 'Ready': return d === 'Pickup' ? 'st_ReadyPickup' : 'st_Ready'
    case 'OnTheWay': return 'st_OnTheWay'
    case 'Completed': return d === 'Pickup' ? 'st_PickedUp' : 'st_Delivered'
    case 'Cancelled': return 'st_Cancelled'
  }
}
export const statusText = (s: OrderStatus, d: DeliveryType) => t(statusKey(s, d))

/** Progress steps shown on the order page; pickup orders skip "В пути". */
export function steps(d: DeliveryType): OrderStatus[] {
  return d === 'Pickup' ? ['New', 'InProgress', 'Ready', 'Completed'] : ['New', 'InProgress', 'Ready', 'OnTheWay', 'Completed']
}
export function stepIndex(s: OrderStatus, d: DeliveryType) {
  return steps(d).indexOf(s === 'Overdue' ? 'New' : s)
}
export const tone = (s: OrderStatus) =>
  s === 'Cancelled' ? 'grey' : s === 'Completed' ? 'green' : s === 'Ready' || s === 'OnTheWay' ? 'blue' : 'amber'
