<script setup lang="ts">
import { ref, watch } from 'vue'
import { catalogApi, type IkpuRef, type IkpuRow } from '../../api'
import BranchSwitcher from '../../components/BranchSwitcher.vue'
import Icon from '../../components/Icon.vue'
import { loc } from '../../format'
import { catalogBranch } from '../../store'

interface Row extends IkpuRow { draft: { ikpu: string; packageCode: string; unitCode: string }; state: '' | 'saving' | 'saved' | 'error'; msg?: string }

const rows = ref<Row[] | null>(null)
const reference = ref<IkpuRef[]>([])
const missing = ref(0)
const missingOnly = ref(false)
const search = ref('')
const bulkMsg = ref('')

async function load() {
  const r = await catalogApi.ikpu({ search: search.value.trim(), missingOnly: missingOnly.value, branchId: catalogBranch.value })
  reference.value = r.reference
  missing.value = r.missing
  rows.value = r.items.map(i => ({
    ...i, state: '',
    draft: { ikpu: i.ikpu ?? '', packageCode: i.packageCode ?? '', unitCode: i.unitCode ?? '' },
  }))
}
let timer = 0
watch(search, () => { clearTimeout(timer); timer = window.setTimeout(load, 300) })
watch([missingOnly, catalogBranch], load, { immediate: true })

const dirty = (r: Row) =>
  r.draft.ikpu !== (r.ikpu ?? '') || r.draft.packageCode !== (r.packageCode ?? '') || r.draft.unitCode !== (r.unitCode ?? '')

async function save(r: Row) {
  r.state = 'saving'
  try {
    const res = await catalogApi.saveIkpu(r.id, {
      ikpu: r.draft.ikpu.trim() || null, packageCode: r.draft.packageCode.trim() || null, unitCode: r.draft.unitCode.trim() || null,
    })
    Object.assign(r, res)
    r.state = 'saved'
    missing.value = rows.value!.filter(x => !x.ikpu).length
  } catch (e) {
    r.state = 'error'
    r.msg = (e as Error).message
  }
}

/** "Сгенерировать ИКПУ": fills the row's inputs; the merchant reviews and saves. */
async function suggest(r: Row) {
  try {
    const s = await catalogApi.suggestIkpu(r.id)
    r.draft = { ikpu: s.ikpu, packageCode: s.packageCode, unitCode: s.unitCode }
    r.ikpuName = s.ikpuName
    r.state = ''
  } catch (e) {
    r.state = 'error'
    r.msg = (e as Error).message
  }
}

async function generateAll() {
  const res = await catalogApi.generateMissingIkpu()
  bulkMsg.value = `Подобрано: ${res.filled}` + (res.remaining ? `, не удалось подобрать: ${res.remaining}` : '')
  load()
}

const refName = (code: string) => reference.value.find(e => e.code === code)?.name
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>ИКПУ</h1>
      <BranchSwitcher />
      <button class="btn btn-primary" :disabled="!missing" @click="generateAll">
        <Icon name="sparkles" />Сгенерировать ИКПУ для всех без кода{{ missing ? ` (${missing})` : '' }}
      </button>
    </div>

    <p class="notice">
      ИКПУ (идентификационный код продукции и услуг) печатается в фискальном чеке. В прототипе подбор идёт по
      <b>демо-справочнику</b> по ключевым словам — в рабочей версии запрос уходит в классификатор
      tasnif.soliq.uz, а продавец подтверждает найденный код.
    </p>
    <p v-if="bulkMsg" class="ok-msg">{{ bulkMsg }}</p>

    <div class="card">
      <div class="toolbar">
        <label class="search">
          <Icon name="search" />
          <input v-model="search" class="input" placeholder="Товар или код ИКПУ" aria-label="Поиск" />
        </label>
        <label class="switch"><input v-model="missingOnly" type="checkbox" /><span class="track" />Только без ИКПУ</label>
      </div>
      <div class="table-wrap">
        <table class="data">
          <thead>
            <tr><th>Товар</th><th>ИКПУ (17 цифр)</th><th>Код упаковки</th><th>Код ед. изм.</th><th class="right">Действия</th></tr>
          </thead>
          <tbody>
            <tr v-for="r in rows" :key="r.id">
              <td>
                <b>{{ loc(r.name) }}</b>
                <div class="faint small">{{ loc(r.category) || 'Без категории' }} · {{ r.unit }}</div>
              </td>
              <td>
                <input v-model="r.draft.ikpu" class="input code" :class="{ missing: !r.draft.ikpu }" inputmode="numeric" maxlength="17"
                       placeholder="Не указан" :aria-label="`ИКПУ: ${loc(r.name)}`" @keydown.enter="save(r)" />
                <div class="faint small ref">{{ refName(r.draft.ikpu) ?? r.ikpuName ?? '' }}</div>
              </td>
              <td><input v-model="r.draft.packageCode" class="input short" aria-label="Код упаковки" @keydown.enter="save(r)" /></td>
              <td><input v-model="r.draft.unitCode" class="input short" aria-label="Код единицы измерения" @keydown.enter="save(r)" /></td>
              <td class="right nowrap">
                <button class="btn btn-sm btn-ghost" title="Подобрать ИКПУ по названию" @click="suggest(r)"><Icon name="sparkles" />Сгенерировать</button>
                <button class="btn btn-sm" :class="{ 'btn-primary': dirty(r) }" :disabled="!dirty(r) || r.state === 'saving'" @click="save(r)">
                  {{ r.state === 'saved' && !dirty(r) ? 'Сохранено ✓' : 'Сохранить' }}
                </button>
                <div v-if="r.state === 'error'" class="err small">{{ r.msg }}</div>
              </td>
            </tr>
            <tr v-if="rows && !rows.length"><td colspan="5" class="empty">Нет товаров</td></tr>
            <tr v-if="!rows"><td colspan="5"><div class="skeleton" style="height: 240px" /></td></tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>

<style scoped>
.notice { margin: 0 0 14px; padding: 10px 14px; border-radius: 10px; background: var(--info-bg); color: #194c8a; font-size: 13px; max-width: 900px; }
.ok-msg { margin: 0 0 14px; color: var(--good); font-weight: 600; }
.toolbar { display: flex; gap: 14px; align-items: center; padding: 12px 14px; border-bottom: 1px solid var(--border); flex-wrap: wrap; }
.code { width: 190px; font-variant-numeric: tabular-nums; letter-spacing: .02em; }
.code.missing { border-color: #f1c27d; background: #fffaf0; }
.short { width: 90px; }
.ref { max-width: 240px; margin-top: 2px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.err { color: var(--bad); }
.small { font-size: 12px; }
</style>
