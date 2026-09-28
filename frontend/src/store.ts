import { ref, watch } from 'vue'
import { api, chatApi, type Lookups } from './api'
import { adminToken } from './auth'

// Reference data (branches, employees, store info) is loaded once and shared across views.
const lookups = ref<Lookups | null>(null)
let pending: Promise<void> | null = null

function load() {
  pending = api.lookups()
    .then(l => {
      lookups.value = l
      // The remembered branch may belong to a store this browser was signed in to before: its products would all
      // look missing. Anything that isn't one of this store's branches falls back to «Все филиалы».
      if (catalogBranch.value !== '' && !l.branches.some(b => b.id === catalogBranch.value)) setCatalogBranch('')
    })
    .catch(() => { pending = null })
  return pending
}

export function useLookups() {
  if (!lookups.value && !pending) load()
  return { lookups }
}

/** Reloads branches and store info after something changed them (e.g. the AI assistant added a branch). */
export const refreshLookups = () => load()

// Signing out and into another store must not keep the previous store's branches, name or selected branch.
watch(adminToken, () => {
  lookups.value = null
  pending = null
  setCatalogBranch('')
})

export function botLink(username?: string) {
  return username ? `https://t.me/${username}` : '#'
}

// ---- Products section: every sub-page is shown for one selected branch ("в разрезе выбранного филиала").
function readBranch(): number | '' {
  try {
    const v = localStorage.getItem('plum.catalogBranch')
    return v ? Number(v) : ''
  } catch {
    return ''
  }
}
export const catalogBranch = ref<number | ''>(readBranch())
export function setCatalogBranch(v: number | '') {
  catalogBranch.value = v
  try { localStorage.setItem('plum.catalogBranch', String(v)) } catch { /* private mode */ }
}

// ---- Chat unread badge in the sidebar, refreshed in the background.
export const chatUnread = ref(0)
let unreadTimer = 0
export function watchChatUnread() {
  const tick = () => chatApi.unread().then(r => { chatUnread.value = r.count }).catch(() => {})
  tick()
  clearInterval(unreadTimer)
  unreadTimer = window.setInterval(tick, 15_000)
}
export const refreshChatUnread = () => chatApi.unread().then(r => { chatUnread.value = r.count }).catch(() => {})

// ---- New storefront orders waiting for the store (sidebar badge on "Заказы").
export const newOrders = ref(0)
let newOrdersTimer = 0
export const refreshNewOrders = () =>
  api.orders({ tab: 'new', pageSize: 5 }).then(r => { newOrders.value = r.counts.new ?? 0 }).catch(() => {})
export function watchNewOrders() {
  refreshNewOrders()
  clearInterval(newOrdersTimer)
  newOrdersTimer = window.setInterval(refreshNewOrders, 20_000)
}
