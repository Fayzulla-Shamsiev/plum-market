import { ref } from 'vue'
import { api, chatApi, type Lookups } from './api'

// Reference data (branches, employees, store info) is loaded once and shared across views.
const lookups = ref<Lookups | null>(null)
let pending: Promise<void> | null = null

export function useLookups() {
  if (!lookups.value && !pending) {
    pending = api.lookups()
      .then(l => { lookups.value = l })
      .catch(() => { pending = null })
  }
  return { lookups }
}

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
