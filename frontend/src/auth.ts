import { computed, ref } from 'vue'

// Admin-panel session (spec "Вход и регистрация администратора"). The browser keeps only a bearer token;
// everything the panel loads afterwards is scoped to the store behind that token.

export type StorePlatform = 'Website' | 'Telegram'

/** The merchant's own bot, once its token has been checked with Telegram. */
export interface StoreBot {
  username: string
  name: string
  url: string
  linkedAt: string | null
  /** Why the "Open Shop" button isn't attached yet. */
  warning: string | null
  /** What the bot's "Open Shop" button opens. */
  buttonUrl: string
  /** True when that is the published prototype rather than this shop (a local run can't be opened from Telegram). */
  buttonIsFallback?: boolean
}

export interface AdminStore {
  id: number
  name: string
  slug: string
  platform: StorePlatform
  /** Where customers open this shop. */
  url: string
  bot: StoreBot | null
}
export interface Admin { id: number; name: string; phone: string; store: AdminStore }

const KEY = 'plum.admin.token'

function read() {
  try { return localStorage.getItem(KEY) ?? '' } catch { return '' }
}
function write(token: string) {
  try { token ? localStorage.setItem(KEY, token) : localStorage.removeItem(KEY) } catch { /* private mode */ }
}

export const adminToken = ref(read())
export const admin = ref<Admin | null>(null)
export const signedIn = computed(() => !!adminToken.value)

/** Set by main.ts: where to send the panel when the session turns out to be gone. */
let onSignedOut: (() => void) | null = null
export const setSignOutHandler = (fn: () => void) => { onSignedOut = fn }

/** The ready-made accounts of the demo store, when this installation has one. */
export interface DemoAccounts {
  storeSlug: string
  storeName: string
  admin: { phone: string; password: string }
  customer: { phone: string; name: string; favorites: number[]; viewed: number[] }
}

export const demo = ref<DemoAccounts | null>(null)
let demoLoaded = false

export async function loadDemo() {
  if (demoLoaded) return demo.value
  demoLoaded = true
  try {
    const res = await fetch('/api/auth/demo')
    demo.value = res.status === 200 ? await res.json() as DemoAccounts : null
  } catch {
    demo.value = null
  }
  return demo.value
}

export class AuthError extends Error {
  field?: string
  constructor(message: string, field?: string) {
    super(message)
    this.field = field
  }
}

async function post<T>(path: string, body: unknown): Promise<T> {
  const res = await fetch(`/api/auth/${path}`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
  })
  const data = await res.json().catch(() => null) as { error?: string; field?: string } | null
  if (!res.ok) throw new AuthError(data?.error ?? `Ошибка ${res.status}`, data?.field)
  return data as T
}

function start(session: { token: string; admin: Admin }) {
  adminToken.value = session.token
  admin.value = session.admin
  write(session.token)
  return session.admin
}

export const login = (phone: string, password: string) =>
  post<{ token: string; admin: Admin }>('login', { phone, password }).then(start)

export const register = (
  name: string, phone: string, password: string, storeName: string,
  platform: StorePlatform, botToken?: string,
) => post<{ token: string; admin: Admin }>('register', { name, phone, password, storeName, platform, botToken }).then(start)

/** Loads the administrator behind a stored token after a page reload; false means the session is gone. */
export async function restore(): Promise<boolean> {
  if (!adminToken.value) return false
  const res = await fetch('/api/auth/me', { headers: { Authorization: `Bearer ${adminToken.value}` } })
  if (!res.ok) {
    forget()
    return false
  }
  admin.value = await res.json() as Admin
  return true
}

export function forget() {
  adminToken.value = ''
  admin.value = null
  write('')
}

export async function logout() {
  const token = adminToken.value
  forget()
  if (token) await fetch('/api/auth/logout', { method: 'POST', headers: { Authorization: `Bearer ${token}` } }).catch(() => {})
}

/** Called by the API client when the server rejects the session mid-session. */
export function sessionExpired() {
  if (!adminToken.value) return
  forget()
  onSignedOut?.()
}
