<script setup lang="ts">
import { ref } from 'vue'
import { catalogApi } from '../api'
import Icon from './Icon.vue'

/** One uploadable image (category picture, banner). */
withDefaults(defineProps<{ label?: string; wide?: boolean }>(), { label: 'Загрузить изображение', wide: false })
const url = defineModel<string | null>({ required: true })
const input = ref<HTMLInputElement>()
const busy = ref(false)
const error = ref('')

async function pick(ev: Event) {
  const f = (ev.target as HTMLInputElement).files?.[0]
  ;(ev.target as HTMLInputElement).value = ''
  if (!f) return
  busy.value = true
  error.value = ''
  try {
    const u = await catalogApi.upload(f)
    if (u.type !== 'image') throw new Error('Нужно изображение')
    url.value = u.url
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div>
    <div class="img" :class="{ wide }">
      <template v-if="url">
        <img :src="url" alt="" />
        <button type="button" class="rm" aria-label="Удалить изображение" @click="url = null"><Icon name="trash" /></button>
      </template>
      <button v-else type="button" class="add" :disabled="busy" @click="input?.click()">
        <Icon name="image" /><span>{{ busy ? 'Загрузка…' : label }}</span>
      </button>
    </div>
    <input ref="input" type="file" accept="image/*" hidden @change="pick" />
    <p v-if="error" class="err">{{ error }}</p>
  </div>
</template>

<style scoped>
.img { position: relative; width: 140px; aspect-ratio: 1; border-radius: 12px; overflow: hidden; border: 1px solid var(--border); background: var(--surface-2); }
.img.wide { width: 100%; aspect-ratio: 3 / 1; }
.img img { width: 100%; height: 100%; object-fit: cover; display: block; }
.add { width: 100%; height: 100%; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 6px; border: 1.5px dashed var(--border-strong); border-radius: 12px; background: none; cursor: pointer; font: inherit; font-size: 12px; color: var(--text-2); }
.add:hover { border-color: var(--plum-500); color: var(--plum-600); }
.add svg { width: 22px; height: 22px; }
.rm { position: absolute; right: 6px; top: 6px; width: 28px; height: 28px; border: 0; border-radius: 7px; background: rgb(255 255 255 / 92%); cursor: pointer; display: grid; place-items: center; }
.rm svg { width: 15px; height: 15px; }
.err { color: var(--bad); font-size: 12px; margin: 6px 0 0; }
</style>
