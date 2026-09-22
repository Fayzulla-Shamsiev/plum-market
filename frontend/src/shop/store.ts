import { ref, watch } from 'vue'
import { shopApi, type CategoryNode, type Meta, type StoreInfo } from './api'
import { lang } from './i18n'

// Shell data (store name, branches, category tree), loaded once and reloaded when the language changes.
export const meta = ref<Meta | null>(null)
let started = false

export function useMeta() {
  if (!started) {
    started = true
    watch(lang, () => shopApi.meta().then(m => { meta.value = m }).catch(() => { started = false }), { immediate: true })
  }
  return meta
}

/** Top-level category that contains the given category. */
export function rootOf(categories: CategoryNode[], id: number): CategoryNode | undefined {
  const contains = (n: CategoryNode): boolean => n.id === id || n.children.some(contains)
  return categories.find(contains)
}

// Store info for "О нас", terms pages and "Связаться с нами", loaded once.
const info = ref<StoreInfo | null>(null)
let infoStarted = false
export function useInfo() {
  if (!infoStarted) {
    infoStarted = true
    shopApi.info().then(i => { info.value = i }).catch(() => { infoStarted = false })
  }
  return info
}
