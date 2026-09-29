import { ref } from 'vue'
import { markTourDone } from './auth'

// Guided tour of the admin panel. The panel dims, one feature at a time is spotlit, and a box explains what it does
// and what it keeps. It starts by itself on an administrator's first visit to the panel and can be replayed from
// «Обучение» in the sidebar.

export const tourActive = ref(false)
/** On a phone the sidebar is a drawer: the tour opens it for menu steps and closes it for page steps. */
export const tourSidebar = ref(false)

export function startTour() {
  tourActive.value = true
}

export function endTour() {
  tourActive.value = false
  tourSidebar.value = false
  markTourDone()
}
