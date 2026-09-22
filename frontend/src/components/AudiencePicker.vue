<script setup lang="ts">
import { ref, watch } from 'vue'
import { api, marketingApi, type AudienceFilter, type CustomerRow, type Platform } from '../api'
import { count, languageLabel, platformLabel } from '../format'
import Icon from './Icon.vue'
import PlatformIcon from './PlatformIcon.vue'

/**
 * Chooses recipients either by segment (platform, language, activity…) or by hand-picking customers.
 * `channel` decides who counts as reachable: Telegram-bot users for broadcasts, everyone with a phone for SMS.
 */
const props = withDefaults(defineProps<{ channel?: 'bot' | 'sms'; reachHint?: string }>(), { channel: 'bot', reachHint: '' })
const model = defineModel<AudienceFilter>({ required: true })
const stats = defineModel<{ total: number; reachable: number }>('stats')

const mode = ref<'segment' | 'manual'>(model.value.customerIds?.length ? 'manual' : 'segment')
const platforms = Object.keys(platformLabel) as Platform[]
const languages = ['ru', 'uz', 'en']
const reachLabel = props.channel === 'sms' ? 'с номером телефона' : 'получат сообщение в боте'

function toggle<T>(list: T[] | undefined, v: T): T[] {
  const s = new Set(list ?? [])
  s.has(v) ? s.delete(v) : s.add(v)
  return [...s]
}
const numberOrNull = (ev: Event) => {
  const v = (ev.target as HTMLInputElement).value
  return v ? Number(v) : null
}

// --- manual pick
const search = ref('')
const found = ref<CustomerRow[]>([])
const picked = ref<Map<number, string>>(new Map())
let timer = 0
watch(search, () => {
  clearTimeout(timer)
  timer = window.setTimeout(async () => {
    found.value = search.value.trim().length >= 2 ? (await api.customers({ search: search.value.trim(), pageSize: 20 })).items : []
  }, 250)
})
function pick(id: number, name: string) {
  const m = new Map(picked.value)
  m.has(id) ? m.delete(id) : m.set(id, name)
  picked.value = m
  model.value = { customerIds: [...m.keys()] }
}
function setMode(m: 'segment' | 'manual') {
  mode.value = m
  model.value = m === 'manual' ? { customerIds: [...picked.value.keys()] } : {}
}

// Recount whenever the filter changes (debounced).
const loading = ref(false)
let countTimer = 0
watch(model, f => {
  clearTimeout(countTimer)
  if (mode.value === 'manual' && !f.customerIds?.length) { stats.value = { total: 0, reachable: 0 }; return }
  countTimer = window.setTimeout(async () => {
    loading.value = true
    try {
      const r = await marketingApi.audience(f)
      stats.value = { total: r.total, reachable: props.channel === 'sms' ? r.total : r.reachable }
    } finally {
      loading.value = false
    }
  }, 250)
}, { deep: true, immediate: true })
</script>

<template>
  <div class="aud">
    <div class="seg" role="tablist">
      <button type="button" role="tab" :class="{ on: mode === 'segment' }" @click="setMode('segment')">По параметрам</button>
      <button type="button" role="tab" :class="{ on: mode === 'manual' }" @click="setMode('manual')">Выбрать вручную</button>
    </div>

    <div v-if="mode === 'segment'" class="grid">
      <div class="field">
        <span>Платформа</span>
        <div class="chips">
          <button v-for="p in platforms" :key="p" type="button" class="chip" :class="{ active: model.platforms?.includes(p) }"
                  @click="model = { ...model, platforms: toggle(model.platforms, p) }">
            <PlatformIcon :platform="p" show-label />
          </button>
        </div>
      </div>
      <div class="field">
        <span>Язык клиента</span>
        <div class="chips">
          <button v-for="l in languages" :key="l" type="button" class="chip" :class="{ active: model.languages?.includes(l) }"
                  @click="model = { ...model, languages: toggle(model.languages, l) }">{{ languageLabel[l] }}</button>
        </div>
      </div>
      <label class="field"><span>Минимум заказов</span>
        <input class="input" type="number" min="0" :value="model.minOrders ?? ''" placeholder="любое"
               @input="model = { ...model, minOrders: numberOrNull($event) }" />
      </label>
      <label class="field"><span>Были активны за последние, дней</span>
        <input class="input" type="number" min="1" :value="model.lastVisitDays ?? ''" placeholder="за всё время"
               @input="model = { ...model, lastVisitDays: numberOrNull($event) }" />
      </label>
      <label class="field"><span>Бонусных баллов не меньше</span>
        <input class="input" type="number" min="0" :value="model.minBonus ?? ''" placeholder="любое"
               @input="model = { ...model, minBonus: numberOrNull($event) }" />
      </label>
      <p class="faint small">Пустые условия — все клиенты.</p>
    </div>

    <div v-else class="manual">
      <label class="search"><Icon name="search" /><input v-model="search" class="input" placeholder="Имя или телефон (от 2 символов)" aria-label="Найти клиента" /></label>
      <ul v-if="found.length" class="found">
        <li v-for="c in found" :key="c.id">
          <label class="pick"><input type="checkbox" :checked="picked.has(c.id)" @change="pick(c.id, c.fullName)" />
            <span class="grow">{{ c.fullName }}</span><span class="faint small num">{{ c.phone }}</span><PlatformIcon :platform="c.platform" /></label>
        </li>
      </ul>
      <div v-if="picked.size" class="picked">
        <span v-for="[id, name] in picked" :key="id" class="tag">{{ name }}<button type="button" :aria-label="`Убрать ${name}`" @click="pick(id, name)">×</button></span>
      </div>
    </div>

    <div class="summary" :class="{ dim: loading }">
      <div><b class="num">{{ count(stats?.total ?? 0) }}</b> клиентов выбрано</div>
      <div><b class="num ok">{{ count(stats?.reachable ?? 0) }}</b> {{ reachLabel }}</div>
      <p v-if="reachHint && stats && stats.total > stats.reachable" class="hint">{{ reachHint }}</p>
    </div>
  </div>
</template>

<style scoped>
.aud { display: flex; flex-direction: column; gap: 14px; }
.seg { display: inline-grid; grid-template-columns: 1fr 1fr; background: var(--bg); padding: 3px; border-radius: 9px; align-self: flex-start; }
.seg button { border: 0; background: none; font: inherit; font-size: 13px; font-weight: 550; padding: 6px 14px; border-radius: 7px; cursor: pointer; color: var(--text-2); }
.seg button.on { background: var(--surface); color: var(--text); box-shadow: var(--shadow); }
.grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
.chips { display: flex; flex-wrap: wrap; gap: 6px; }
.chip.active :deep(.pf) { color: #fff; }
.manual .search { display: block; }
.manual .search .input { width: 100%; }
.found { list-style: none; margin: 8px 0 0; padding: 4px; border: 1px solid var(--border); border-radius: 10px; max-height: 240px; overflow-y: auto; }
.pick { display: flex; align-items: center; gap: 8px; padding: 6px 8px; border-radius: 8px; cursor: pointer; }
.pick:hover { background: var(--surface-2); }
.pick input { accent-color: var(--plum-600); width: 16px; height: 16px; }
.picked { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 10px; }
.tag { display: inline-flex; align-items: center; gap: 4px; background: var(--plum-100); color: var(--plum-700); border-radius: 12px; padding: 2px 4px 2px 10px; font-size: 12px; font-weight: 600; }
.tag button { border: 0; background: none; cursor: pointer; color: inherit; font-size: 15px; line-height: 1; padding: 0 4px; }
.summary { display: flex; gap: 24px; flex-wrap: wrap; align-items: baseline; padding: 12px 14px; border-radius: 10px; background: var(--plum-50); transition: opacity .15s; }
.summary b { font-size: 20px; }
.summary .ok { color: var(--good); }
.summary.dim { opacity: .6; }
.hint { flex-basis: 100%; margin: 0; font-size: 12px; color: var(--warn); }
.small { font-size: 12px; }
@media (max-width: 640px) { .grid { grid-template-columns: 1fr; } }
</style>
