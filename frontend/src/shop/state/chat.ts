import { ref } from 'vue'
import { persisted } from './persist'

// Visitor chat with the store. The token identifies this browser's thread in the admin inbox.
function newToken() {
  return (crypto.randomUUID?.() ?? `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}-${Math.random().toString(36).slice(2)}`)
}
export const chatToken = persisted<string>('plum.shop.chatToken', newToken())
export const chatName = persisted<string>('plum.shop.chatName', '')
export const chatOpen = ref(false)
/** Product the visitor is asking about (set when the chat is opened from a product page). */
export const chatProduct = ref<{ id: number; name: string; price: number; image: string | null } | null>(null)

/** Order the customer is asking about (support chat, opened from an order page). */
export const chatOrder = ref<number | null>(null)

export function openChat(product: typeof chatProduct.value = null, orderId: number | null = null) {
  chatProduct.value = product
  chatOrder.value = product ? null : orderId
  chatOpen.value = true
}
