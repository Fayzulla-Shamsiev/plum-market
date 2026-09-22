<script setup lang="ts">
import { ref } from 'vue'
import { catalogApi, type Media } from '../api'
import Icon from './Icon.vue'

/** Photos + videos for a product. The first image is the cover shown in lists. */
const props = withDefaults(defineProps<{ allowVideo?: boolean; max?: number }>(), { allowVideo: true, max: 10 })
const media = defineModel<Media[]>({ required: true })
const input = ref<HTMLInputElement>()
const uploading = ref(0)
const error = ref('')

async function onFiles(ev: Event) {
  const files = Array.from((ev.target as HTMLInputElement).files ?? [])
  ;(ev.target as HTMLInputElement).value = ''
  error.value = ''
  for (const f of files.slice(0, props.max - media.value.length)) {
    uploading.value++
    try {
      const u = await catalogApi.upload(f)
      if (u.type === 'image' || (u.type === 'video' && props.allowVideo)) media.value = [...media.value, { url: u.url, type: u.type }]
      else error.value = `${f.name}: этот тип файла здесь не поддерживается`
    } catch (e) {
      error.value = (e as Error).message
    } finally {
      uploading.value--
    }
  }
}

function remove(i: number) { media.value = media.value.filter((_, idx) => idx !== i) }
function makeCover(i: number) {
  const copy = [...media.value]
  const [m] = copy.splice(i, 1)
  media.value = [m, ...copy]
}
</script>

<template>
  <div>
    <div class="grid">
      <div v-for="(m, i) in media" :key="m.url" class="tile">
        <img v-if="m.type === 'image'" :src="m.url" alt="" />
        <video v-else :src="m.url" muted />
        <span v-if="i === 0" class="cover">Обложка</span>
        <span v-if="m.type === 'video'" class="vid"><Icon name="video" /></span>
        <div class="actions">
          <button v-if="i > 0" type="button" title="Сделать обложкой" aria-label="Сделать обложкой" @click="makeCover(i)"><Icon name="star" /></button>
          <button type="button" title="Удалить" aria-label="Удалить" @click="remove(i)"><Icon name="trash" /></button>
        </div>
      </div>
      <button v-if="media.length < max" type="button" class="tile add" :disabled="uploading > 0" @click="input?.click()">
        <Icon name="upload" />
        <span>{{ uploading ? 'Загрузка…' : allowVideo ? 'Фото или видео' : 'Загрузить' }}</span>
      </button>
    </div>
    <input ref="input" type="file" hidden multiple :accept="allowVideo ? 'image/*,video/*' : 'image/*'" @change="onFiles" />
    <p v-if="error" class="err">{{ error }}</p>
  </div>
</template>

<style scoped>
.grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(104px, 1fr)); gap: 10px; }
.tile { position: relative; aspect-ratio: 1; border-radius: 10px; overflow: hidden; border: 1px solid var(--border); background: var(--surface-2); }
.tile img, .tile video { width: 100%; height: 100%; object-fit: cover; display: block; }
.tile.add { display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 6px; border: 1.5px dashed var(--border-strong); cursor: pointer; color: var(--text-2); font: inherit; font-size: 12px; }
.tile.add:hover { border-color: var(--plum-500); color: var(--plum-600); }
.tile.add svg { width: 22px; height: 22px; }
.cover { position: absolute; left: 6px; top: 6px; background: var(--plum-600); color: #fff; font-size: 10px; font-weight: 700; padding: 2px 6px; border-radius: 6px; }
.vid { position: absolute; left: 6px; bottom: 6px; background: rgb(0 0 0 / 55%); color: #fff; border-radius: 6px; padding: 2px 4px; display: flex; }
.vid svg { width: 14px; height: 14px; }
.actions { position: absolute; right: 6px; top: 6px; display: flex; gap: 4px; opacity: 0; transition: opacity .15s; }
.tile:hover .actions, .tile:focus-within .actions { opacity: 1; }
.actions button { width: 26px; height: 26px; border-radius: 7px; border: 0; background: rgb(255 255 255 / 92%); cursor: pointer; display: grid; place-items: center; }
.actions svg { width: 14px; height: 14px; }
.err { color: var(--bad); font-size: 12px; margin: 6px 0 0; }
</style>
