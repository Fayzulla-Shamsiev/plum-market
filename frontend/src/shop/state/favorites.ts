import { computed } from 'vue'
import { persisted } from './persist'

// Favourites are kept in the browser; they'll move to the customer's account once phone login exists.
export const favoriteIds = persisted<number[]>('plum.shop.favorites', [])
export const favoritesCount = computed(() => favoriteIds.value.length)
export const isFavorite = (id: number) => favoriteIds.value.includes(id)

/** Returns true when the product is now a favourite. */
export function toggleFavorite(id: number): boolean {
  const i = favoriteIds.value.indexOf(id)
  if (i >= 0) {
    favoriteIds.value.splice(i, 1)
    return false
  }
  favoriteIds.value.unshift(id)
  return true
}
