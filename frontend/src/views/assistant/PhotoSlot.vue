<script setup lang="ts">
import { ref } from 'vue'
import { api, catalogApi } from '../../api'
import Icon from '../../components/Icon.vue'

// One photo for a product or a category, straight from the assistant's cards: pick a file, it is uploaded and set
// as the main photo — the same one the product form and the storefront show.
const props = defineProps<{ kind: 'product' | 'category'; id: number; url: string | null; label: string; size?: number }>()
const emit = defineEmits<{ changed: [url: string | null] }>()

const input = ref<HTMLInputElement>()
const busy = ref(false)
const error = ref('')

async function pick(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0]
  ;(e.target as HTMLInputElement).value = ''
  if (!file) return
  if (!file.type.startsWith('image/')) {
    error.value = 'Нужна картинка'
    return
  }
  busy.value = true
  error.value = ''
  try {
    const up = await catalogApi.upload(file)
    await api.setPhoto(props.kind, props.id, up.url)
    emit('changed', up.url)
  } catch (err) {
    error.value = (err as Error).message
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <button type="button" class="slot" :class="{ has: !!url, busy }" :style="{ '--s': `${size ?? 44}px` }"
    :title="error || (url ? `Заменить фото «${label}»` : `Добавить фото «${label}»`)" :aria-label="url ? `Заменить фото «${label}»` : `Добавить фото «${label}»`"
    :disabled="busy" @click="input?.click()">
    <img v-if="url" :src="url" alt="" />
    <span v-else class="empty"><Icon name="image" /><i>+</i></span>
    <span v-if="busy" class="spin" />
    <span v-if="error" class="err">!</span>
    <input ref="input" type="file" accept="image/*" hidden @change="pick" />
  </button>
</template>

<style scoped>
.slot {
  position: relative; flex: none; width: var(--s); height: var(--s); padding: 0; border-radius: 10px; cursor: pointer; overflow: hidden;
  border: 1.5px dashed #c3cedd; background: #f6f9fd; display: grid; place-items: center; color: var(--blue, #1f7aec);
  transition: border-color .15s, background .15s;
}
.slot:hover { border-color: var(--blue, #1f7aec); background: #eef5ff; }
.slot.has { border-style: solid; border-color: var(--line, #e2e8f1); }
.slot img { width: 100%; height: 100%; object-fit: cover; }
.empty { position: relative; display: grid; place-items: center; color: #1f7aec; }
.empty svg { width: 18px; height: 18px; }
.empty i { position: absolute; right: -7px; bottom: -6px; font-style: normal; font-weight: 800; font-size: 12px; line-height: 1; }
.spin { position: absolute; inset: 0; background: rgb(255 255 255 / 70%); }
.spin::after { content: ''; position: absolute; inset: 30%; border-radius: 50%; border: 2px solid var(--blue, #1f7aec); border-right-color: transparent; animation: r .7s linear infinite; }
@keyframes r { to { transform: rotate(360deg); } }
.err { position: absolute; top: 2px; right: 2px; width: 14px; height: 14px; border-radius: 50%; background: #d92d20; color: #fff; font-size: 10px; font-weight: 800; display: grid; place-items: center; }
</style>
