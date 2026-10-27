<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { api, type PhotoChoice } from '../../api'
import Icon from '../../components/Icon.vue'

// Card «photos»: free stock photos from Pixabay the assistant found for products or categories without a photo.
// A tap only selects; «Сохранить фото» copies the selected ones into the shop and tells the assistant, so the
// conversation moves on by itself. Saved picks stay marked after a reload. Pixabay's thumbnails are only for
// choosing — once the search is a day old they may no longer load, the saved photos still do.
const props = defineProps<{ title: string; sets: PhotoChoice[]; locked?: boolean }>()
const emit = defineEmits<{ saved: [names: string[]] }>()

const rows = reactive(props.sets.map(s => ({ ...s, pending: null as number | null, error: '', stale: false })))
const broken = ref(new Set<string>())
const saving = ref<{ done: number; total: number } | null>(null)

const pending = computed(() => rows.filter(r => r.pending !== null))

function select(row: (typeof rows)[number], photoId: number) {
  if (saving.value) return
  row.error = ''
  row.pending = row.pending === photoId || row.chosenId === photoId ? null : photoId
}

/** Highlighted: the new selection, or else what is already saved. */
const marked = (row: (typeof rows)[number]) => row.pending ?? row.chosenId

async function save() {
  if (saving.value || !pending.value.length) return
  const queue = [...pending.value]
  const saved: string[] = []
  const progress = { done: 0, total: queue.length }
  saving.value = progress
  // One after another: each save also updates the same card on the server.
  for (const row of queue) {
    try {
      const r = await api.pickStockPhoto(row.kind, row.id, row.pending!)
      row.chosenId = row.pending
      row.chosenUrl = r.url
      row.pending = null
      saved.push(row.name)
    } catch (e) {
      row.error = (e as Error).message
    }
    progress.done++
    saving.value = { ...progress }
  }
  saving.value = null
  if (saved.length) emit('saved', saved)
}

function cancel() {
  for (const r of rows) r.pending = null
}

function lost(row: (typeof rows)[number], thumb: string) {
  broken.value = new Set(broken.value).add(thumb)
  if (row.photos.every(p => broken.value.has(p.thumb))) row.stale = true
}
</script>

<template>
  <div class="sp">
    <div class="sp-title"><Icon name="image" />{{ title }}</div>
    <section v-for="r in rows" :key="`${r.kind}${r.id}`" class="set">
      <div class="set-head">
        <span class="name">{{ r.name }}</span>
        <small>{{ r.kind === 'category' ? 'категория' : 'товар' }}</small>
        <span v-if="r.pending !== null" class="state picked">Выбрано</span>
        <span v-else-if="r.chosenId" class="state done"><Icon name="check" />Сохранено</span>
      </div>
      <p v-if="r.stale" class="stale">
        <img v-if="r.chosenUrl" :src="r.chosenUrl" alt="" />
        {{ r.chosenUrl ? 'Выбранное фото уже в магазине.' : 'Подборка устарела — попросите помощника найти фото ещё раз.' }}
      </p>
      <div v-else class="strip">
        <button v-for="p in r.photos" v-show="!broken.has(p.thumb)" :key="p.id" type="button" class="ph"
          :class="{ on: marked(r) === p.id, saved: r.pending === null && r.chosenId === p.id }" :disabled="!!saving"
          :aria-pressed="marked(r) === p.id" :title="p.author ? `Фото: ${p.author}, Pixabay` : 'Pixabay'" @click="select(r, p.id)">
          <img :src="p.thumb" alt="" loading="lazy" @error="lost(r, p.thumb)" />
          <span v-if="marked(r) === p.id" class="badge"><Icon name="check" /></span>
        </button>
      </div>
      <p v-if="r.error" class="err">{{ r.error }}</p>
    </section>

    <div v-if="pending.length || saving" class="confirm">
      <button type="button" class="save" :disabled="!!saving || locked" @click="save">
        <span v-if="saving" class="spin" />
        {{ saving ? `Сохраняю ${saving.done + 1} из ${saving.total}…` : `Сохранить фото (${pending.length})` }}
      </button>
      <button v-if="!saving" type="button" class="undo" @click="cancel">Отменить</button>
    </div>
    <p v-else class="foot">
      Нажмите на фото, чтобы выбрать, или пропустите. Бесплатные фото с
      <a href="https://pixabay.com/" target="_blank" rel="noopener">Pixabay</a>.
    </p>
  </div>
</template>

<style scoped>
.sp { display: flex; flex-direction: column; gap: 12px; }
.sp-title { display: flex; align-items: center; gap: 8px; font-weight: 650; }
.sp-title svg { width: 18px; height: 18px; color: var(--blue); }
.set { display: flex; flex-direction: column; gap: 7px; }
.set + .set { padding-top: 12px; border-top: 1px solid var(--line); }
.set-head { display: flex; align-items: baseline; gap: 6px; flex-wrap: wrap; }
.name { font-weight: 600; font-size: 14px; }
.set-head small { color: var(--ink-3); font-size: 12px; }
.state { margin-left: auto; display: inline-flex; align-items: center; gap: 4px; font-size: 12.5px; font-weight: 600; }
.state svg { width: 14px; height: 14px; }
.picked { color: var(--blue); }
.done { color: #139a55; }
/* one row per item, scrolled sideways on a phone */
.strip { display: flex; gap: 8px; overflow-x: auto; padding: 3px 3px 4px; margin: -3px; scrollbar-width: thin; }
.ph { position: relative; flex: none; width: 92px; height: 92px; padding: 0; border: 0; border-radius: 12px; overflow: hidden; background: #edf1f7; cursor: pointer; outline: 2px solid transparent; outline-offset: 1px; transition: outline-color .15s, transform .15s, opacity .15s; }
.ph img { width: 100%; height: 100%; object-fit: cover; display: block; }
.ph:hover:not(:disabled) { transform: translateY(-1px); outline-color: var(--blue-100); }
.ph:disabled { cursor: default; }
.ph.on { outline: 3px solid var(--blue); }
.ph.saved { outline-color: #35d07f; }
.badge { position: absolute; top: 6px; right: 6px; width: 22px; height: 22px; border-radius: 50%; display: grid; place-items: center; background: var(--blue); color: #fff; box-shadow: 0 2px 6px rgb(0 0 0 / 25%); }
.ph.saved .badge { background: #139a55; }
.badge svg { width: 13px; height: 13px; }
.stale { margin: 0; display: flex; align-items: center; gap: 10px; font-size: 13px; color: var(--ink-3); }
.stale img { width: 52px; height: 52px; border-radius: 10px; object-fit: cover; }
.err { margin: 0; color: #b42318; font-size: 13px; }
.foot { margin: 0; font-size: 12.5px; color: var(--ink-3); }
.foot a { color: var(--ink-2); text-decoration: underline; }
.confirm { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; padding-top: 2px; }
.save { height: 40px; display: inline-flex; align-items: center; gap: 8px; padding: 0 18px; border: 0; border-radius: 11px; background: var(--blue); color: #fff; font: inherit; font-size: 14px; font-weight: 650; cursor: pointer; }
.save:hover:not(:disabled) { background: var(--blue-600); }
.save:disabled { opacity: .75; cursor: default; }
.undo { border: 0; background: none; padding: 4px 2px; font: inherit; font-size: 13.5px; font-weight: 600; color: var(--ink-3); cursor: pointer; }
.undo:hover { color: var(--blue); }
.spin { width: 16px; height: 16px; border-radius: 50%; border: 2.5px solid rgb(255 255 255 / 45%); border-top-color: #fff; animation: turn .8s linear infinite; }
@keyframes turn { to { transform: rotate(360deg); } }
</style>
