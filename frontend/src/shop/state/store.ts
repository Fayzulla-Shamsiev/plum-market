import { persisted } from './persist'
import { authToken } from './auth'
import { cartLines } from './cart'
import { favoriteIds } from './favorites'
import { viewedIds } from './viewed'

// Which shop the visitor is in. One installation hosts many stores (every administrator gets their own at
// registration), so the storefront carries the shop's address — /shop/{slug} — into every request as X-Store.
export const storeSlug = persisted<string>('plum.shop.store', '')

/** Opening another shop means another catalog, another cart and another account. */
export function openStore(slug: string) {
  if (storeSlug.value === slug) return
  authToken.value = ''
  cartLines.value = []
  favoriteIds.value = []
  viewedIds.value = []
  storeSlug.value = slug
}
