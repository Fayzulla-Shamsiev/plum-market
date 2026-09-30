import { computed, ref } from 'vue'
import { markTourSeen } from './auth'
import { tours } from './tours'

// Guided tours of the admin panel (see tours.ts): a short one per page — it starts by itself the first time the
// administrator opens that page and is replayed with «?» next to the page title — and the full walkthrough of the
// whole panel, from «?» at the bottom of the menu.

/** The page whose tour is running, or null. */
export const tourPage = ref<string | null>(null)
export const tourActive = computed(() => tourPage.value !== null)
/** On a phone the sidebar is a drawer: the tour opens it for steps that point into the menu. */
export const tourSidebar = ref(false)

export function startTour(page: string) {
  if (tours[page]?.length) tourPage.value = page
}

/** Ends the tour. <paramref name="covered"/>: pages the full walkthrough showed, whose own tours needn't pop up now. */
export function endTour(covered: string[] = []) {
  const page = tourPage.value
  tourPage.value = null
  tourSidebar.value = false
  for (const p of new Set([page, ...covered])) if (p) markTourSeen(p)
}
