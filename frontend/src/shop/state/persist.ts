import { ref, watch, type Ref } from 'vue'

/** A ref mirrored to localStorage. Storage may be unavailable (private mode), so the ref still works without it. */
export function persisted<T>(key: string, initial: T): Ref<T> {
  let start = initial
  try {
    const raw = localStorage.getItem(key)
    if (raw) start = JSON.parse(raw) as T
  } catch { /* unreadable or blocked */ }
  const r = ref(start) as Ref<T>
  watch(r, v => {
    try { localStorage.setItem(key, JSON.stringify(v)) } catch { /* blocked */ }
  }, { deep: true })
  // Keep several open tabs in sync.
  window.addEventListener('storage', e => {
    if (e.key !== key || e.newValue === null) return
    try { r.value = JSON.parse(e.newValue) as T } catch { /* ignore */ }
  })
  return r
}
