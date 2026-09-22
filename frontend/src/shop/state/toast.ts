import { ref } from 'vue'

// One short confirmation at a time ("Добавлено в корзину"), with an optional link.
export const toast = ref<{ id: number; text: string; to?: string; action?: string } | null>(null)
let timer: number | undefined

export function showToast(text: string, to?: string, action?: string) {
  clearTimeout(timer)
  toast.value = { id: Date.now(), text, to, action }
  timer = window.setTimeout(() => { toast.value = null }, 2600)
}
