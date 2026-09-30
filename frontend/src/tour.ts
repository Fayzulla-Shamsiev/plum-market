import { computed, ref } from 'vue'
import { markTourSeen } from './auth'
import { tours } from './tours'

// Guided tours of the admin panel, one per page (see tours.ts). A page's tour starts by itself the first time the
// administrator opens it and can be replayed with «?» in the sidebar.

/** The page whose tour is running, or null. */
export const tourPage = ref<string | null>(null)
export const tourActive = computed(() => tourPage.value !== null)
/** On a phone the sidebar is a drawer: the tour opens it for steps that point into the menu. */
export const tourSidebar = ref(false)

export function startTour(page: string) {
  if (tours[page]?.length) tourPage.value = page
}

export function endTour() {
  const page = tourPage.value
  tourPage.value = null
  tourSidebar.value = false
  if (page) markTourSeen(page)
}
