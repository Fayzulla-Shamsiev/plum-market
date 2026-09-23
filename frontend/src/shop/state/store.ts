import { persisted } from './persist'
import { authToken } from './auth'
import { cartLines } from './cart'
import { favoriteIds } from './favorites'
import { viewedIds } from './viewed'

// Which shop the visitor is in. Every administrator gets their own shop at registration, and in production each
// one answers on its own address; on this shared prototype host the browser remembers the shop opened through
// /shop/{slug} and sends it as X-Store. There is no listing of shops — a shop is reached by its address only.
export const storeSlug = persisted<string>('plum.shop.store', '')

/**
 * Asks the server which shop this address belongs to (its subdomain, or the only/demo shop of a prototype
 * host). Returns false when the address names no shop.
 */
export async function resolveStore(): Promise<boolean> {
  if (storeSlug.value) return true
  try {
    const res = await fetch('/api/shop/current')
    if (!res.ok) return false
    storeSlug.value = (await res.json() as { slug: string }).slug
    return true
  } catch {
    return false
  }
}

/** Opening another shop means another catalog, another cart and another account. */
export function openStore(slug: string) {
  if (storeSlug.value === slug) return
  authToken.value = ''
  cartLines.value = []
  favoriteIds.value = []
  viewedIds.value = []
  storeSlug.value = slug
}
