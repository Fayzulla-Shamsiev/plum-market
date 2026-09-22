<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { catalogApi, download, type ImportResult, type ImportSettings } from '../../api'
import Icon from '../../components/Icon.vue'
import Modal from '../../components/Modal.vue'

const emit = defineEmits<{ close: []; imported: [] }>()
const tab = ref<'file' | 'external'>('file')

// --- file import
const file = ref<File | null>(null)
const busy = ref(false)
const result = ref<ImportResult | null>(null)
const error = ref('')
const drag = ref(false)

function pick(ev: Event) {
  file.value = (ev.target as HTMLInputElement).files?.[0] ?? null
  result.value = null
}
function drop(ev: DragEvent) {
  drag.value = false
  file.value = ev.dataTransfer?.files?.[0] ?? null
  result.value = null
}

async function run() {
  if (!file.value) return
  busy.value = true
  error.value = ''
  try {
    result.value = await catalogApi.importProducts(file.value)
    emit('imported')
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busy.value = false
  }
}

// --- external source parameters
const sources = ['Billz', 'МойСклад', '1С', 'iiko', 'Poster', 'Smartup', 'Другой (API)']
const settings = ref<ImportSettings>({ source: null, url: null, apiKey: null, autoSync: false })
const savedMsg = ref('')
onMounted(async () => { settings.value = await catalogApi.importSettings() })
async function saveSettings() {
  settings.value = await catalogApi.saveImportSettings(settings.value)
  savedMsg.value = 'Параметры сохранены'
}
</script>

<template>
  <Modal title="Импорт товаров" width="560px" persistent @close="emit('close')">
    <div class="tabs">
      <button class="chip" :class="{ active: tab === 'file' }" @click="tab = 'file'">Из файла Excel</button>
      <button class="chip" :class="{ active: tab === 'external' }" @click="tab = 'external'">Внешний источник</button>
    </div>

    <div v-if="tab === 'file'" class="stack">
      <div class="step">
        <b>1. Скачайте шаблон</b>
        <p class="muted">Пример Excel-таблицы с нужными колонками и инструкцией.</p>
        <button class="btn btn-sm" @click="download(catalogApi.importTemplateUrl)"><Icon name="download" />Скачать шаблон .xlsx</button>
      </div>
      <div class="step">
        <b>2. Загрузите заполненный файл</b>
        <label class="drop" :class="{ drag }" @dragover.prevent="drag = true" @dragleave="drag = false" @drop.prevent="drop">
          <input type="file" accept=".xlsx" hidden @change="pick" />
          <Icon name="upload" />
          <span v-if="file"><b>{{ file.name }}</b> · {{ Math.round(file.size / 1024) }} КБ</span>
          <span v-else>Перетащите файл сюда или <u>выберите</u></span>
        </label>
      </div>
      <div v-if="error" class="error-banner">{{ error }}</div>
      <div v-if="result" class="report">
        <div class="row"><span class="ok">Создано: <b>{{ result.created }}</b></span><span class="ok">Обновлено: <b>{{ result.updated }}</b></span>
          <span v-if="result.errors.length" class="bad">Ошибок: <b>{{ result.errors.length }}</b></span></div>
        <ul v-if="result.errors.length"><li v-for="e in result.errors" :key="e">{{ e }}</li></ul>
      </div>
    </div>

    <div v-else class="stack">
      <p class="muted">Параметры для автоматической загрузки каталога из учётной системы. Подключение синхронизации — в разделе «Расширения».</p>
      <label class="field"><span>Система</span>
        <select v-model="settings.source" class="select">
          <option :value="null">Не выбрано</option>
          <option v-for="s in sources" :key="s" :value="s">{{ s }}</option>
        </select>
      </label>
      <label class="field"><span>Адрес API / ссылка на выгрузку</span>
        <input v-model="settings.url" class="input" placeholder="https://…" />
      </label>
      <label class="field"><span>Ключ доступа</span>
        <input v-model="settings.apiKey" class="input" type="password" autocomplete="off" placeholder="Токен из учётной системы" />
      </label>
      <label class="switch"><input v-model="settings.autoSync" type="checkbox" /><span class="track" />Синхронизировать автоматически раз в час</label>
      <p v-if="savedMsg" class="saved">{{ savedMsg }}</p>
    </div>

    <template #footer>
      <button class="btn" @click="emit('close')">{{ result ? 'Готово' : 'Отмена' }}</button>
      <button v-if="tab === 'file'" class="btn btn-primary" :disabled="!file || busy" @click="run">{{ busy ? 'Импорт…' : 'Импортировать' }}</button>
      <button v-else class="btn btn-primary" @click="saveSettings">Сохранить параметры</button>
    </template>
  </Modal>
</template>

<style scoped>
.tabs { display: flex; gap: 8px; margin-bottom: 16px; }
.stack { display: flex; flex-direction: column; gap: 16px; }
.step p { margin: 2px 0 8px; font-size: 13px; }
.drop { margin-top: 8px; display: flex; align-items: center; justify-content: center; gap: 10px; padding: 26px; border: 1.5px dashed var(--border-strong); border-radius: 12px; cursor: pointer; color: var(--text-2); }
.drop:hover, .drop.drag { border-color: var(--plum-500); background: var(--plum-50); }
.drop svg { width: 22px; height: 22px; }
.report { padding: 12px 14px; border-radius: 10px; background: var(--surface-2); border: 1px solid var(--border); }
.report .row { gap: 16px; }
.ok { color: var(--good); }
.bad { color: var(--bad); }
.report ul { margin: 8px 0 0; padding-left: 18px; font-size: 13px; color: var(--bad); max-height: 140px; overflow: auto; }
.saved { margin: 0; color: var(--good); font-size: 13px; }
</style>
