<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { marketingApi, type SourceRow } from '../../api'
import Icon from '../../components/Icon.vue'
import Modal from '../../components/Modal.vue'
import PlatformIcon from '../../components/PlatformIcon.vue'
import { count, date, relative } from '../../format'

const rows = ref<SourceRow[] | null>(null)
const editing = ref<SourceRow | 'new' | null>(null)
const form = ref<{ type: 'Telegram' | 'Website'; name: string }>({ type: 'Telegram', name: '' })
const error = ref('')
const copied = ref('')

async function load() { rows.value = await marketingApi.sources() }
onMounted(load)

function open(s: SourceRow | 'new') {
  editing.value = s
  form.value = s === 'new' ? { type: 'Telegram', name: '' } : { type: s.type, name: s.name }
  error.value = ''
}
async function save() {
  error.value = ''
  try {
    if (editing.value === 'new') await marketingApi.createSource(form.value)
    else if (editing.value) await marketingApi.renameSource(editing.value.id, form.value)
    editing.value = null
    load()
  } catch (e) {
    error.value = (e as Error).message
  }
}
async function remove(s: SourceRow) {
  if (!confirm(`Удалить источник «${s.name}»? Опубликованные ссылки перестанут считаться.`)) return
  await marketingApi.deleteSource(s.id)
  load()
}
async function copy(text: string) {
  try { await navigator.clipboard.writeText(text); copied.value = text; setTimeout(() => (copied.value = ''), 1500) } catch { /* blocked */ }
}
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>Источники</h1>
      <button class="btn btn-primary" @click="open('new')"><Icon name="plus" />Добавить источник</button>
    </div>
    <p class="muted intro">Создайте отдельную ссылку для каждой рекламы, поста или QR-кода — и смотрите, откуда приходят клиенты и заказы.</p>
    <div class="card">
      <div class="table-wrap">
        <table class="data">
          <thead>
            <tr>
              <th>Тип</th><th>Название</th><th class="right">Клики</th><th class="right">Новые</th><th class="right">Существующие</th>
              <th class="right">Заказы</th><th class="right">Конверсия</th><th>Создан</th><th>Посл. визит</th><th class="right">Действия</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="s in rows" :key="s.id">
              <td><PlatformIcon :platform="s.type" show-label /></td>
              <td>
                <b>{{ s.name }}</b>
                <div class="links">
                  <button class="link" :title="'Скопировать ссылку для публикации'" @click="copy(s.link)">
                    <Icon :name="copied === s.link ? 'check' : 'clipboard'" />{{ s.link.replace('https://', '') }}
                  </button>
                </div>
              </td>
              <td class="right num">{{ count(s.clicks) }}</td>
              <td class="right num">{{ count(s.newUsers) }}</td>
              <td class="right num">{{ count(s.existingUsers) }}</td>
              <td class="right num"><b>{{ count(s.orders) }}</b></td>
              <td class="right num">{{ s.conversion.toLocaleString('ru-RU') }}%</td>
              <td class="num nowrap">{{ date(s.createdAt) }}</td>
              <td class="nowrap">{{ s.lastVisitAt ? relative(s.lastVisitAt) : '—' }}</td>
              <td class="right nowrap">
                <a :href="s.trackedLink" target="_blank" rel="noopener" class="btn btn-sm btn-ghost btn-icon" title="Открыть короткую ссылку (засчитает клик)" aria-label="Открыть ссылку"><Icon name="chevronRight" /></a>
                <button class="btn btn-sm btn-ghost btn-icon" title="Переименовать" aria-label="Переименовать" @click="open(s)"><Icon name="edit" /></button>
                <button class="btn btn-sm btn-ghost btn-icon btn-danger" title="Удалить" aria-label="Удалить" @click="remove(s)"><Icon name="trash" /></button>
              </td>
            </tr>
            <tr v-if="rows && !rows.length"><td colspan="10" class="empty">Источников пока нет</td></tr>
            <tr v-if="!rows"><td colspan="10"><div class="skeleton" style="height: 200px" /></td></tr>
          </tbody>
        </table>
      </div>
    </div>

    <Modal v-if="editing" :title="editing === 'new' ? 'Новый источник' : 'Переименовать источник'" width="460px" persistent @close="editing = null">
      <div class="stack">
        <div v-if="editing === 'new'" class="field">
          <span>Куда ведёт ссылка</span>
          <div class="types">
            <label :class="{ on: form.type === 'Telegram' }"><input v-model="form.type" type="radio" value="Telegram" /><PlatformIcon platform="Telegram" /><b>Telegram-бот</b><small>t.me/бот?start=…</small></label>
            <label :class="{ on: form.type === 'Website' }"><input v-model="form.type" type="radio" value="Website" /><PlatformIcon platform="Website" /><b>Веб-сайт</b><small>сайт?utm_source=…</small></label>
          </div>
        </div>
        <label class="field"><span>Название</span><input v-model="form.name" class="input" placeholder="Например: Реклама у блогера" @keydown.enter="save" /></label>
        <p v-if="editing !== 'new'" class="faint small">Ссылка останется прежней — уже опубликованные ссылки продолжат работать.</p>
        <div v-if="error" class="error-banner">{{ error }}</div>
      </div>
      <template #footer>
        <button class="btn" @click="editing = null">Отмена</button>
        <button class="btn btn-primary" :disabled="!form.name.trim()" @click="save">Сохранить</button>
      </template>
    </Modal>
  </div>
</template>

<style scoped>
.intro { margin: -8px 0 14px; }
.links { margin-top: 3px; }
.link { display: inline-flex; align-items: center; gap: 5px; border: 0; background: none; padding: 0; font: 12px ui-monospace, SFMono-Regular, Menlo, monospace; color: var(--plum-600); cursor: pointer; max-width: 260px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.link svg { width: 12px; height: 12px; flex: none; }
.stack { display: flex; flex-direction: column; gap: 14px; }
.types { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
.types label { border: 1px solid var(--border-strong); border-radius: 10px; padding: 10px 12px; cursor: pointer; display: flex; flex-direction: column; gap: 4px; }
.types input { position: absolute; opacity: 0; }
.types small { color: var(--text-3); font-size: 11px; font-family: ui-monospace, monospace; }
.types .on { border-color: var(--plum-500); background: var(--plum-50); box-shadow: 0 0 0 1px var(--plum-500); }
.small { font-size: 12px; margin: 0; }
</style>
