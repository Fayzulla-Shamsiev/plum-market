import { ref } from 'vue'
import type { Router } from 'vue-router'

/**
 * Telegram Mini App support (spec «Telegram Mini App»): the same storefront the website uses, opened inside the
 * merchant's bot by its "Open Shop" button. Telegram loads it in a phone-sized web view and hands the page its
 * own SDK, so here we only do what a web page can't do by itself — tell Telegram we're ready, take the whole
 * height, paint its header like our storefront, and use its back button for navigation.
 *
 * Nothing here runs in a browser: without Telegram's launch parameters the SDK is never even loaded.
 */
interface ThemeParams { bg_color?: string; secondary_bg_color?: string }

interface WebApp {
  initData: string
  initDataUnsafe?: { user?: { first_name?: string; last_name?: string; username?: string } }
  themeParams?: ThemeParams
  ready(): void
  expand(): void
  setHeaderColor?(color: string): void
  setBackgroundColor?(color: string): void
  disableVerticalSwipes?(): void
  BackButton?: { show(): void; hide(): void; onClick(fn: () => void): void }
  onEvent?(event: string, fn: () => void): void
}

declare global {
  interface Window { Telegram?: { WebApp?: WebApp } }
}

export const inTelegram = ref(false)
/** The Telegram account that opened the shop — used to offer a name at sign-in (Telegram never gives a phone). */
export const telegramName = ref('')

/** Storefront colours, so Telegram's own chrome matches the page instead of the user's chat theme. */
const PAGE = '#f4f6fa'
const HEADER = '#ffffff'

const launchedFromTelegram = () =>
  /tgWebApp(Data|Platform|Version)/.test(location.hash) || !!window.Telegram?.WebApp?.initData

function load(): Promise<void> {
  if (window.Telegram?.WebApp) return Promise.resolve()
  return new Promise(resolve => {
    const script = document.createElement('script')
    script.src = 'https://telegram.org/js/telegram-web-app.js'
    script.onload = () => resolve()
    script.onerror = () => resolve() // fall back to the plain storefront
    document.head.appendChild(script)
  })
}

/** Called before the app mounts, so the first paint is already the Mini App one. */
export async function initTelegram(): Promise<void> {
  if (!launchedFromTelegram()) return
  await load()
  const tg = window.Telegram?.WebApp
  if (!tg) return

  inTelegram.value = true
  document.documentElement.classList.add('tg')
  // An old Telegram client refuses methods it doesn't know; the shop itself must open anyway.
  try {
    tg.ready()
    tg.expand()
    tg.setHeaderColor?.(HEADER)
    tg.setBackgroundColor?.(PAGE)
    // Otherwise a swipe down while scrolling the catalog closes the shop.
    tg.disableVerticalSwipes?.()
    tg.onEvent?.('themeChanged', () => {
      try {
        tg.setHeaderColor?.(HEADER)
        tg.setBackgroundColor?.(PAGE)
      } catch { /* unsupported on this client */ }
    })
  } catch (e) {
    console.warn('Telegram Mini App setup partly unsupported', e)
  }

  const user = tg.initDataUnsafe?.user
  telegramName.value = [user?.first_name, user?.last_name].filter(Boolean).join(' ').trim()
}

/** Telegram's own back button walks the storefront's history; on the home page it disappears. */
export function bindTelegramBackButton(router: Router) {
  const back = window.Telegram?.WebApp?.BackButton
  if (!inTelegram.value || !back) return
  back.onClick(() => router.back())
  router.afterEach(to => {
    try {
      if (to.path === '/') back.hide()
      else back.show()
    } catch { /* unsupported on this client */ }
  })
}
