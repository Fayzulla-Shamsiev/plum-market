<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { shopApi, type Suggest } from '../api'
import { lang, price, t } from '../i18n'
import { emojiFor, tintFor } from '../visuals'
import SIcon from './SIcon.vue'

const router = useRouter()
const route = useRoute()
const q = ref(typeof route.query.q === 'string' && route.path === '/search' ? route.query.q : '')
const open = ref(false)
const data = ref<Suggest | null>(null)
const loading = ref(false)
const active = ref(-1)
const input = ref<HTMLInputElement>()
const root = ref<HTMLElement>()
// Close when focus leaves the whole widget (tabbing away), not when it moves into the dropdown.
const onFocusOut = (e: FocusEvent) => { if (!root.value?.contains(e.relatedTarget as Node)) open.value = false }

// ---- Recent searches (this browser only) ----
const RECENT_KEY = 'plum.shop.recent'
function readRecent(): string[] {
  try { return JSON.parse(localStorage.getItem(RECENT_KEY) ?? '[]') } catch { return [] }
}
const recent = ref<string[]>(readRecent())
function remember(term: string) {
  recent.value = [term, ...recent.value.filter(x => x.toLowerCase() !== term.toLowerCase())].slice(0, 6)
  try { localStorage.setItem(RECENT_KEY, JSON.stringify(recent.value)) } catch { /* private mode */ }
}
function clearRecent() {
  recent.value = []
  try { localStorage.removeItem(RECENT_KEY) } catch { /* private mode */ }
}

// ---- Type-ahead ----
let timer: number | undefined
let ctrl: AbortController | undefined
watch([q, lang], () => {
  active.value = -1
  clearTimeout(timer)
  ctrl?.abort()
  const term = q.value.trim()
  if (!term) { data.value = null; loading.value = false; return }
  loading.value = true
  timer = window.setTimeout(async () => {
    ctrl = new AbortController()
    try {
      data.value = await shopApi.suggest(term, ctrl.signal)
      loading.value = false
    } catch (e) {
      if ((e as Error).name !== 'AbortError') loading.value = false
    }
  }, 180)
})

// Keep the box in sync with the search page (e.g. Back button) and empty it once you leave search.
watch(() => route.fullPath, () => {
  q.value = route.path === '/search' && typeof route.query.q === 'string' ? route.query.q : ''
  open.value = false
})

// Flat option list so arrow keys walk through recent terms, categories and products in display order.
type Option = { key: string; go: () => void }
const options = computed<Option[]>(() => {
  if (!q.value.trim()) return recent.value.map(r => ({ key: 'r' + r, go: () => submit(r) }))
  if (!data.value) return []
  return [
    ...data.value.categories.map(c => ({ key: 'c' + c.id, go: () => go(`/catalog/${c.id}`) })),
    ...data.value.products.map(p => ({ key: 'p' + p.id, go: () => go(`/product/${p.id}`) })),
    ...(data.value.total ? [{ key: 'all', go: () => submit() }] : []),
  ]
})
const idx = (key: string) => options.value.findIndex(o => o.key === key)

function go(to: Parameters<typeof router.push>[0]) {
  if (q.value.trim()) remember(q.value.trim())
  open.value = false
  input.value?.blur()
  router.push(to)
}
function submit(term = q.value) {
  const v = term.trim()
  if (!v) return
  q.value = v
  go({ path: '/search', query: { q: v } })
}
function onKey(e: KeyboardEvent) {
  const n = options.value.length
  if (e.key === 'ArrowDown' && n) { e.preventDefault(); open.value = true; active.value = (active.value + 1) % n }
  else if (e.key === 'ArrowUp' && n) { e.preventDefault(); active.value = (active.value - 1 + n) % n }
  // Enter on a highlighted option opens it; otherwise the form's own submit runs the search.
  else if (e.key === 'Enter' && !e.isComposing && active.value >= 0) { e.preventDefault(); options.value[active.value]?.go() }
  else if (e.key === 'Escape') { open.value = false; input.value?.blur() }
}

// Bold the part of a name that matches what was typed.
function parts(name: string) {
  const term = q.value.trim()
  const i = term ? name.toLowerCase().indexOf(term.toLowerCase()) : -1
  return i < 0 ? [name, '', ''] : [name.slice(0, i), name.slice(i, i + term.length), name.slice(i + term.length)]
}

defineExpose({ focus: () => input.value?.focus() })
</script>

<template>
  <div ref="root" class="sbox" @focusout="onFocusOut">
    <form class="sbox-field" role="search" @submit.prevent="submit()">
      <SIcon name="search" class="lead" />
      <input ref="input" v-model="q" type="search" :placeholder="t('searchPlaceholder')" autocomplete="off" enterkeyhint="search"
        role="combobox" aria-autocomplete="list" :aria-expanded="open" aria-controls="search-pop" @focus="open = true" @keydown="onKey" />
      <button v-if="q" type="button" class="clear" :aria-label="t('clear')" @click="q = ''; input?.focus()"><SIcon name="close" :size="16" /></button>
      <button type="submit" class="go">{{ t('search') }}</button>
    </form>

    <div v-if="open && (q.trim() || recent.length)" id="search-pop" class="pop" role="listbox" @mousedown.prevent>
      <!-- Empty box: recent searches -->
      <template v-if="!q.trim()">
        <div class="pop-head">{{ t('recent') }} <button type="button" class="link" @click="clearRecent">{{ t('clear') }}</button></div>
        <button v-for="r in recent" :key="r" type="button" class="opt recent" :class="{ hl: options[active]?.key === 'r' + r }"
          role="option" @click="submit(r)"><SIcon name="clock" :size="16" /> {{ r }}</button>
      </template>

      <template v-else-if="data && (data.categories.length || data.products.length)">
        <button v-for="c in data.categories" :key="'c' + c.id" type="button" class="opt cat" :class="{ hl: active === idx('c' + c.id) }"
          role="option" @click="go(`/catalog/${c.id}`)">
          <span class="cat-ico">{{ emojiFor(c.name) }}</span>
          <span class="grow"><span class="nm">{{ c.name }}</span><small>{{ c.path }}</small></span>
          <SIcon name="chevronRight" :size="16" class="faint" />
        </button>
        <div v-if="data.categories.length && data.products.length" class="sep" />
        <button v-for="p in data.products" :key="'p' + p.id" type="button" class="opt prod" :class="{ hl: active === idx('p' + p.id) }"
          role="option" @click="go(`/product/${p.id}`)">
          <span class="thumb" :style="{ background: tintFor(p.id) }">
            <img v-if="p.image" :src="p.image" alt="" />
            <template v-else>{{ emojiFor(p.name) }}</template>
          </span>
          <span class="grow nm">{{ parts(p.name)[0] }}<b>{{ parts(p.name)[1] }}</b>{{ parts(p.name)[2] }}</span>
          <span class="pr" :class="{ sale: p.oldPrice }">{{ price(p.price) }}</span>
        </button>
        <button type="button" class="opt all" :class="{ hl: active === idx('all') }" role="option" @click="submit()">
          {{ t('showAllResults') }} · {{ data.total }}
          <SIcon name="chevronRight" :size="16" />
        </button>
      </template>

      <div v-else-if="!loading && data" class="none">
        <strong>{{ t('nothingFound') }}</strong>
        <span>{{ t('nothingFoundHint') }}</span>
      </div>
      <div v-else class="none faint">…</div>
    </div>
  </div>
</template>

<style scoped>
.sbox { position: relative; flex: 1; min-width: 0; max-width: 640px; }
.sbox-field {
  display: flex; align-items: center; height: 46px; background: var(--page); border: 1.5px solid transparent;
  border-radius: 14px; padding: 0 4px 0 14px; transition: border-color .15s, background .15s, box-shadow .15s;
}
.sbox-field:focus-within { background: var(--card); border-color: var(--blue); box-shadow: 0 0 0 4px var(--blue-50); }
.lead { color: var(--ink-3); flex: none; }
input { flex: 1; min-width: 0; border: 0; background: none; outline: none; font: inherit; padding: 0 10px; color: var(--ink); height: 100%; }
input::-webkit-search-cancel-button { display: none; }
.shop input:focus-visible { outline: none; }
.clear { border: 0; background: none; color: var(--ink-3); cursor: pointer; width: 30px; height: 30px; display: grid; place-items: center; border-radius: 8px; }
.clear:hover { background: var(--line); color: var(--ink); }
.go { height: 36px; padding: 0 16px; border: 0; border-radius: 10px; background: var(--blue); color: #fff; font-weight: 600; cursor: pointer; margin-left: 4px; }
.go:hover { background: var(--blue-600); }

.pop {
  position: absolute; left: 0; right: 0; top: calc(100% + 8px); background: var(--card); border-radius: 16px;
  box-shadow: var(--lift-lg); border: 1px solid var(--line); padding: 8px; z-index: 60; max-height: min(70vh, 560px); overflow-y: auto;
}
.pop-head { display: flex; justify-content: space-between; align-items: center; padding: 6px 10px; font-size: 13px; color: var(--ink-3); font-weight: 600; }
.link { border: 0; background: none; color: var(--blue); cursor: pointer; font-size: 13px; }
.opt {
  display: flex; align-items: center; gap: 12px; width: 100%; border: 0; background: none; text-align: left; cursor: pointer;
  padding: 8px 10px; border-radius: 10px; color: var(--ink);
}
.opt:hover, .opt.hl { background: var(--blue-50); }
.recent { color: var(--ink-2); }
.recent svg { color: var(--ink-3); }
.grow { flex: 1; min-width: 0; }
.cat .grow { display: flex; flex-direction: column; }
.cat small { color: var(--ink-3); font-size: 12.5px; }
.cat-ico { width: 36px; height: 36px; border-radius: 10px; background: var(--blue-50); display: grid; place-items: center; font-size: 19px; flex: none; }
.nm { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.nm b { font-weight: 700; color: var(--blue); }
.thumb { width: 40px; height: 40px; border-radius: 10px; display: grid; place-items: center; font-size: 22px; flex: none; overflow: hidden; }
.thumb img { width: 100%; height: 100%; object-fit: cover; }
.pr { font-weight: 650; font-size: 14px; white-space: nowrap; }
.pr.sale { color: var(--green); }
.sep { height: 1px; background: var(--line); margin: 6px 10px; }
.all { justify-content: center; color: var(--blue); font-weight: 600; margin-top: 4px; gap: 4px; }
.none { padding: 18px 12px; text-align: center; display: flex; flex-direction: column; gap: 4px; color: var(--ink-2); font-size: 14px; }
.faint { color: var(--ink-3); }
@media (max-width: 640px) {
  .go { display: none; }
  .sbox-field { height: 44px; padding-right: 6px; }
}
</style>
