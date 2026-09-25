<script setup lang="ts">
import { computed, ref } from 'vue'
import { catalogApi, type Attribute, type Lang, type Localized } from '../api'
import { catalogLangLabel, catalogLangs } from '../format'
import Icon from './Icon.vue'

/**
 * Name + description in every catalog language, with the spec's two AI helpers:
 * "Перевести" fills the other languages from the one being edited, and
 * "Сгенерировать и заполнить автоматически" writes the description in all languages.
 */
const props = defineProps<{
  kind: 'product' | 'category'
  context?: { category?: string; attributes?: Attribute[]; unit?: string; weightGrams?: number | null }
}>()
const name = defineModel<Localized>('name', { required: true })
const description = defineModel<Localized>('description', { required: true })

const lang = ref<Lang>('ru')
const busy = ref<'' | 'translate' | 'describe'>('')
const note = ref('')
const noteKind = ref<'info' | 'ok' | 'warn'>('info')

const filled = (l: Lang) => !!name.value[l]?.trim()
const canTranslate = computed(() => !!(name.value[lang.value]?.trim() || description.value[lang.value]?.trim()))

function setName(v: string) { name.value = { ...name.value, [lang.value]: v } }
function setDescription(v: string) { description.value = { ...description.value, [lang.value]: v } }

async function translate() {
  busy.value = 'translate'
  note.value = ''
  try {
    const fields: Record<string, string> = {}
    if (name.value[lang.value]) fields.name = name.value[lang.value]!
    if (description.value[lang.value]) fields.description = description.value[lang.value]!
    const r = await catalogApi.translate(lang.value, fields)
    const n = { ...name.value }
    const d = { ...description.value }
    const done: string[] = []
    for (const [l, f] of Object.entries(r.translations) as [Lang, Record<string, string>][]) {
      if (f.name) n[l] = f.name
      if (f.description) d[l] = f.description
      done.push(catalogLangLabel[l])
    }
    name.value = n
    description.value = d
    noteKind.value = r.note ? 'warn' : 'ok'
    note.value = r.note ?? `Переведено: ${done.join(', ')}${r.provider === 'openai' ? ' (OpenAI)' : ''}`
  } catch (e) {
    noteKind.value = 'warn'
    note.value = (e as Error).message
  } finally {
    busy.value = ''
  }
}

async function generate() {
  busy.value = 'describe'
  note.value = ''
  try {
    const r = await catalogApi.describe({
      name: name.value.ru || name.value[lang.value] || '',
      nameUz: name.value.uz,
      kind: props.kind,
      category: props.context?.category,
      attributes: props.context?.attributes?.filter(a => a.name && a.value),
      unit: props.context?.unit,
      weightGrams: props.context?.weightGrams,
      existing: description.value.ru,
    })
    const d = { ...description.value }
    for (const [l, f] of Object.entries(r.translations) as [Lang, Record<string, string>][])
      if (f.description) d[l] = f.description
    description.value = d
    noteKind.value = r.note ? 'warn' : 'ok'
    note.value = r.note ?? 'Описание сгенерировано на всех языках (OpenAI)'
  } catch (e) {
    noteKind.value = 'warn'
    note.value = (e as Error).message
  } finally {
    busy.value = ''
  }
}
</script>

<template>
  <div class="loc">
    <div class="loc-head">
      <div class="langs" role="tablist">
        <button v-for="l in catalogLangs" :key="l" type="button" role="tab" :aria-selected="lang === l"
                class="lang" :class="{ active: lang === l }" @click="lang = l">
          {{ catalogLangLabel[l] }}<i :class="filled(l) ? 'ok' : 'miss'" :title="filled(l) ? 'Заполнено' : 'Не заполнено'" />
        </button>
      </div>
      <button type="button" class="btn btn-sm" :disabled="!canTranslate || !!busy" @click="translate">
        <Icon name="translate" />{{ busy === 'translate' ? 'Перевожу…' : 'Перевести' }}
      </button>
    </div>

    <label class="field">
      <span>Название ({{ catalogLangLabel[lang] }}){{ lang === 'ru' ? ' *' : '' }}</span>
      <input class="input" :value="name[lang] ?? ''" :placeholder="kind === 'product' ? 'Например: Круассан с миндалём' : 'Например: Выпечка'"
             @input="setName(($event.target as HTMLInputElement).value)" />
    </label>

    <label class="field">
      <span class="row" style="justify-content: space-between">
        Описание ({{ catalogLangLabel[lang] }})
        <button type="button" class="link-btn" :disabled="!(name.ru || name[lang]) || !!busy" @click="generate">
          <Icon name="sparkles" />{{ busy === 'describe' ? 'Генерирую…' : 'Сгенерировать и заполнить автоматически' }}
        </button>
      </span>
      <textarea class="textarea" rows="4" :value="description[lang] ?? ''"
                @input="setDescription(($event.target as HTMLTextAreaElement).value)" />
    </label>

    <p v-if="note" class="note" :class="noteKind">{{ note }}</p>
  </div>
</template>

<style scoped>
.loc { display: flex; flex-direction: column; gap: 12px; }
.loc-head { display: flex; align-items: center; gap: 10px; justify-content: space-between; flex-wrap: wrap; }
.langs { display: inline-flex; background: var(--bg); border-radius: 9px; padding: 3px; }
.lang { border: 0; background: none; font: inherit; font-weight: 550; padding: 5px 12px; border-radius: 7px; cursor: pointer; color: var(--text-2); display: inline-flex; align-items: center; gap: 6px; }
.lang.active { background: var(--surface); color: var(--text); box-shadow: var(--shadow); }
.lang i { width: 7px; height: 7px; border-radius: 50%; }
.lang i.ok { background: var(--good); }
.lang i.miss { background: #d3d1da; }
.link-btn { border: 0; background: none; color: var(--plum-600); font: inherit; font-size: 12px; font-weight: 600; cursor: pointer; display: inline-flex; align-items: center; gap: 4px; padding: 0; }
.link-btn svg { width: 14px; height: 14px; }
.link-btn:disabled { opacity: .45; cursor: default; }
.note { margin: 0; font-size: 12px; padding: 8px 10px; border-radius: 8px; }
.note.ok { background: var(--good-bg); color: var(--good); }
.note.warn { background: var(--warn-bg); color: var(--warn); }
.note.info { background: var(--info-bg); color: var(--info); }
</style>
