import { computed, ref } from 'vue'
import { persisted } from './persist'

// Signed-in customer (phone + name, per the MVP spec). The token is sent as a Bearer header on every shop request.
export interface Me {
  id: number; name: string; firstName: string; lastName: string; phone: string
  email: string | null; country: string | null; birthDate: string | null; gender: 'male' | 'female' | null
  language: string; notifyOrders: boolean; notifyPromos: boolean
  bonusPoints: number; ordersCount: number; activeOrders: number
}

export const authToken = persisted<string>('plum.shop.token', '')
export const me = ref<Me | null>(null)
export const signedIn = computed(() => !!authToken.value)

// Login modal, opened by anything that needs an account; `then` runs after a successful sign-in.
export const loginOpen = ref(false)
let pending: (() => void) | null = null

export function requireLogin(then: () => void) {
  if (signedIn.value) return then()
  pending = then
  loginOpen.value = true
}
export function onSignedIn(token: string, profile: Me) {
  authToken.value = token
  me.value = profile
  loginOpen.value = false
  const next = pending
  pending = null
  next?.()
}
export function cancelLogin() {
  pending = null
  loginOpen.value = false
}
export function signedOut() {
  authToken.value = ''
  me.value = null
}
