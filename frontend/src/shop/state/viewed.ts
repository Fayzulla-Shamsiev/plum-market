import { persisted } from './persist'

// "Вы недавно смотрели": the last 20 product pages opened in this browser, newest first.
export const viewedIds = persisted<number[]>('plum.shop.viewed', [])

export function markViewed(id: number) {
  viewedIds.value = [id, ...viewedIds.value.filter(x => x !== id)].slice(0, 20)
}
