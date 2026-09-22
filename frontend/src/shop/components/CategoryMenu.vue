<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import type { CategoryNode } from '../api'
import { t } from '../i18n'
import { emojiFor } from '../visuals'
import SIcon from './SIcon.vue'

// Catalog menu opened from the header button. Desktop: two panes (hover a category on the left, its
// sub-categories show on the right). Phone: a full-screen sheet that drills down one level at a time.
const props = defineProps<{ categories: CategoryNode[] }>()
const emit = defineEmits<{ close: [] }>()

const activeId = ref<number | null>(props.categories[0]?.id ?? null)
const drilled = ref(false) // phone only: showing the right pane
const active = computed(() => props.categories.find(c => c.id === activeId.value) ?? null)
watch(() => props.categories, cs => { if (!activeId.value) activeId.value = cs[0]?.id ?? null })

const isPhone = () => window.matchMedia('(max-width: 760px)').matches
const router = useRouter()
function pick(c: CategoryNode) {
  activeId.value = c.id
  // Phone drills into sub-categories; desktop (already showing them on hover) and leaf categories open the page.
  if (isPhone() && c.children.length) drilled.value = true
  else { emit('close'); router.push(`/catalog/${c.id}`) }
}

const onKey = (e: KeyboardEvent) => e.key === 'Escape' && emit('close')
onMounted(() => { document.addEventListener('keydown', onKey); document.body.style.overflow = 'hidden' })
onBeforeUnmount(() => { document.removeEventListener('keydown', onKey); document.body.style.overflow = '' })
</script>

<template>
  <div class="scrim" @click.self="emit('close')">
    <div class="menu" :class="{ drilled }" role="dialog" aria-modal="true" :aria-label="t('catalog')">
      <div class="phone-head">
        <button v-if="drilled" class="icon" :aria-label="t('back')" @click="drilled = false"><SIcon name="chevronLeft" /></button>
        <strong>{{ drilled && active ? active.name : t('catalog') }}</strong>
        <button class="icon" :aria-label="t('close')" @click="emit('close')"><SIcon name="close" /></button>
      </div>

      <nav class="roots">
        <button v-for="c in categories" :key="c.id" class="root" :class="{ on: c.id === activeId }"
          @mouseenter="!isPhone() && (activeId = c.id)" @focus="activeId = c.id" @click="pick(c)">
          <span class="ico">{{ emojiFor(c.name) }}</span>
          <span class="nm">{{ c.name }}</span>
          <SIcon name="chevronRight" :size="16" class="chev" />
        </button>
      </nav>

      <section v-if="active" class="pane">
        <RouterLink :to="`/catalog/${active.id}`" class="pane-head" @click="emit('close')">
          <h3>{{ active.name }}</h3>
          <span>{{ t('seeAll') }} · {{ active.productsCount }} <SIcon name="chevronRight" :size="16" /></span>
        </RouterLink>
        <div class="subs">
          <RouterLink v-for="s in active.children" :key="s.id" :to="`/catalog/${s.id}`" class="sub" @click="emit('close')">
            <span class="sub-ico">{{ emojiFor(s.name) }}</span>
            <span class="sub-nm">{{ s.name }}</span>
            <small>{{ s.productsCount }}</small>
          </RouterLink>
        </div>
      </section>
    </div>
  </div>
</template>

<style scoped>
.scrim { position: fixed; inset: 0; z-index: 80; background: rgb(21 32 51 / 35%); animation: fade .15s; }
@keyframes fade { from { opacity: 0; } }
.menu {
  position: absolute; left: 50%; top: 84px; transform: translateX(-50%); width: min(1000px, calc(100% - 40px));
  background: var(--card); border-radius: 20px; box-shadow: var(--lift-lg); display: grid; grid-template-columns: 290px 1fr;
  min-height: 360px; max-height: calc(100vh - 110px); overflow: hidden; animation: drop .18s ease-out;
}
@keyframes drop { from { transform: translate(-50%, -8px); opacity: 0; } }
.phone-head { display: none; }
.roots { padding: 12px; border-right: 1px solid var(--line); overflow-y: auto; background: #fafbfd; }
.root {
  display: flex; align-items: center; gap: 12px; width: 100%; padding: 10px 12px; border: 0; background: none;
  border-radius: 12px; cursor: pointer; text-align: left; font-weight: 500;
}
.root .ico { width: 38px; height: 38px; border-radius: 11px; background: var(--card); display: grid; place-items: center; font-size: 20px; box-shadow: var(--lift); }
.root .nm { flex: 1; }
.root .chev { color: var(--ink-3); }
.root.on { background: var(--blue-50); color: var(--blue); }
.root.on .chev { color: var(--blue); }
.pane { padding: 22px 26px; overflow-y: auto; }
.pane-head { display: flex; align-items: baseline; justify-content: space-between; gap: 12px; margin-bottom: 18px; }
.pane-head h3 { font-size: 22px; margin: 0; letter-spacing: -0.015em; }
.pane-head span { color: var(--blue); font-weight: 600; font-size: 14px; display: inline-flex; align-items: center; gap: 2px; white-space: nowrap; }
.subs { display: grid; grid-template-columns: repeat(auto-fill, minmax(170px, 1fr)); gap: 12px; }
.sub {
  display: flex; flex-direction: column; align-items: flex-start; gap: 6px; padding: 16px; border-radius: 16px;
  background: var(--page); transition: background .15s, transform .15s;
}
.sub:hover { background: var(--blue-50); transform: translateY(-1px); }
.sub-ico { font-size: 30px; }
.sub-nm { font-weight: 600; }
.sub small { color: var(--ink-3); }

@media (max-width: 760px) {
  .scrim { background: var(--card); }
  .menu {
    inset: 0; top: 0; left: 0; transform: none; width: auto; border-radius: 0; max-height: none; box-shadow: none;
    grid-template-columns: 1fr; grid-template-rows: auto 1fr; animation: none;
  }
  .phone-head { display: flex; align-items: center; gap: 8px; padding: 12px 14px; border-bottom: 1px solid var(--line); }
  .phone-head strong { flex: 1; font-size: 17px; }
  .icon { width: 40px; height: 40px; border: 0; background: var(--page); border-radius: 12px; display: grid; place-items: center; cursor: pointer; }
  .roots { border: 0; background: none; padding-bottom: 90px; }
  .pane { display: none; padding: 16px 14px 90px; }
  .drilled .roots { display: none; }
  .drilled .pane { display: block; }
  .pane-head h3 { visibility: hidden; } /* the sheet header already names the category */
  .subs { grid-template-columns: repeat(2, 1fr); gap: 10px; }
  .root.on { background: none; color: inherit; }
}
</style>
