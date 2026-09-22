<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import Icon from './components/Icon.vue'
import { chatUnread, useLookups, watchChatUnread } from './store'

interface Section { path: string; label: string; icon: string; ready?: boolean; children?: { path: string; label: string }[] }

// Full section list from the spec; the rest are placeholders until their iteration.
const sections: Section[] = [
  { path: '/dashboard', label: 'Дашборд', icon: 'dashboard', ready: true },
  { path: '/orders', label: 'Заказы', icon: 'orders', ready: true },
  { path: '/customers', label: 'Клиенты', icon: 'customers', ready: true },
  { path: '/chat', label: 'Чат', icon: 'chat', ready: true },
  {
    path: '/products', label: 'Продукты', icon: 'products', ready: true,
    children: [
      { path: '/products/categories', label: 'Категории' },
      { path: '/products/items', label: 'Продукты' },
      { path: '/products/discounts', label: 'Скидка' },
      { path: '/products/ikpu', label: 'ИКПУ' },
      { path: '/products/stock', label: 'Склад' },
    ],
  },
  {
    path: '/marketing', label: 'Маркетинг', icon: 'marketing', ready: true,
    children: [
      { path: '/marketing/broadcasts', label: 'Рассылка' },
      { path: '/marketing/promocodes', label: 'Промокод' },
      { path: '/marketing/sources', label: 'Источники' },
      { path: '/marketing/sms', label: 'СМС-рассылка' },
      { path: '/marketing/channel-post', label: 'Пост для канала' },
      { path: '/marketing/banners', label: 'Баннер' },
      { path: '/marketing/reviews', label: 'Обзоры' },
    ],
  },
  { path: '/platforms', label: 'Платформы', icon: 'platforms' },
  { path: '/payment', label: 'Способ оплаты', icon: 'payment' },
  { path: '/delivery', label: 'Доставка', icon: 'delivery' },
  { path: '/branches', label: 'Филиалы', icon: 'branches' },
  { path: '/staff', label: 'Сотрудники', icon: 'staff' },
  { path: '/tariff', label: 'Тарифный план', icon: 'tariff' },
  { path: '/extensions', label: 'Расширения (Plum)', icon: 'extensions' },
  { path: '/settings', label: 'Настройки', icon: 'settings' },
]

const { lookups } = useLookups()
const route = useRoute()
const menuOpen = ref(false)
// Per the spec, clicking a group ("Продукты", "Маркетинг") only expands its sub-menu; the page doesn't change.
const currentGroup = computed(() => sections.find(s => s.children && route.path.startsWith(s.path))?.path ?? null)
const expanded = ref<string | null>(currentGroup.value)

watch(() => route.fullPath, () => {
  menuOpen.value = false
  if (currentGroup.value) expanded.value = currentGroup.value
})
// The unread-chat badge belongs to the admin sidebar, so polling starts on the first admin page only.
const isShop = computed(() => !!route.matched[0]?.meta.shop)
const stopAdminWatch = watch(() => route.matched.length && !isShop.value, admin => {
  if (!admin) return
  watchChatUnread()
  queueMicrotask(() => stopAdminWatch())
}, { immediate: true })

const toggle = (path: string) => (expanded.value = expanded.value === path ? null : path)
</script>

<template>
  <!-- The storefront has its own layout (ShopLayout); everything else is the merchant admin. -->
  <RouterView v-if="isShop" />
  <div v-else class="shell">
    <aside class="sidebar" :class="{ open: menuOpen }">
      <div class="brand">
        <svg viewBox="0 0 32 32" width="30" height="30" aria-hidden="true">
          <rect width="32" height="32" rx="8" fill="#6b2d8c" />
          <circle cx="16" cy="18" r="8" fill="#c58be0" />
          <path d="M16 10c1-3 4-4 6-4-1 3-3 4-6 4z" fill="#8fd18f" />
        </svg>
        <div>
          <div class="brand-name">Plum Market</div>
          <div class="brand-store">{{ lookups?.store.storeName ?? '…' }}</div>
        </div>
      </div>
      <nav>
        <template v-for="s in sections" :key="s.path">
          <template v-if="s.children">
            <button class="nav-item" :class="{ 'group-active': currentGroup === s.path }" :aria-expanded="expanded === s.path" @click="toggle(s.path)">
              <Icon :name="s.icon" />
              <span>{{ s.label }}</span>
              <Icon name="chevronRight" class="chev" :class="{ open: expanded === s.path }" />
            </button>
            <div v-show="expanded === s.path" class="sub">
              <RouterLink v-for="c in s.children" :key="c.path" :to="c.path" class="sub-item">{{ c.label }}</RouterLink>
            </div>
          </template>
          <RouterLink v-else :to="s.path" class="nav-item" :class="{ soon: !s.ready }">
            <Icon :name="s.icon" />
            <span>{{ s.label }}</span>
            <span v-if="s.path === '/chat' && chatUnread" class="badge-count">{{ chatUnread }}</span>
            <small v-if="!s.ready">скоро</small>
          </RouterLink>
        </template>
      </nav>
      <div class="sidebar-foot faint">
        <RouterLink to="/" class="shop-link">Открыть магазин ↗</RouterLink>
        Прототип · v0.4
      </div>
    </aside>
    <div v-if="menuOpen" class="scrim" @click="menuOpen = false" />
    <main class="main">
      <button class="btn btn-ghost btn-icon mobile-menu" aria-label="Меню" @click="menuOpen = true">
        <Icon name="menu" />
      </button>
      <RouterView />
    </main>
  </div>
</template>

<style scoped>
.shell { display: flex; min-height: 100vh; }
.sidebar {
  width: 240px; flex: none; background: var(--plum-900); color: #e9ddf1; position: sticky; top: 0; height: 100vh;
  display: flex; flex-direction: column; padding: 16px 12px; overflow-y: auto; z-index: 30;
}
.brand { display: flex; gap: 10px; align-items: center; padding: 4px 8px 18px; }
.brand-name { font-weight: 700; color: #fff; font-size: 15px; }
.brand-store { font-size: 12px; color: #bba4cb; }
nav { display: flex; flex-direction: column; gap: 2px; }
.nav-item {
  display: flex; align-items: center; gap: 10px; padding: 8px 10px; border-radius: 8px; color: #dccbe8;
  font: inherit; font-weight: 500; text-decoration: none; background: none; border: 0; cursor: pointer; text-align: left; width: 100%;
}
.nav-item svg { width: 18px; height: 18px; flex: none; opacity: .85; }
.nav-item:hover { background: rgb(255 255 255 / 8%); text-decoration: none; color: #fff; }
.nav-item.router-link-active { background: var(--plum-600); color: #fff; }
.nav-item.group-active { color: #fff; }
.nav-item.soon { color: #9c86ad; }
.nav-item small { margin-left: auto; font-size: 10px; text-transform: uppercase; letter-spacing: .04em; color: #85709a; }
.chev { margin-left: auto; width: 14px !important; height: 14px !important; transition: transform .15s; }
.chev.open { transform: rotate(90deg); }
.sub { display: flex; flex-direction: column; gap: 1px; margin: 2px 0 4px 20px; padding-left: 10px; border-left: 1px solid rgb(255 255 255 / 12%); }
.sub-item { padding: 6px 10px; border-radius: 7px; color: #cdb8dc; font-size: 13.5px; }
.sub-item:hover { background: rgb(255 255 255 / 8%); color: #fff; text-decoration: none; }
.sub-item.router-link-active { background: var(--plum-600); color: #fff; }
.badge-count { margin-left: auto; min-width: 20px; height: 20px; padding: 0 6px; border-radius: 10px; background: #e34948; color: #fff; font-size: 11px; font-weight: 700; display: grid; place-items: center; }
.shop-link { display: block; color: #dccbe8; font-weight: 600; margin-bottom: 6px; }
.sidebar-foot { margin-top: auto; padding: 16px 10px 0; font-size: 12px; color: #85709a; }
.main { flex: 1; min-width: 0; position: relative; }
.mobile-menu { display: none; }
.scrim { display: none; }

@media (max-width: 960px) {
  .sidebar { position: fixed; left: 0; top: 0; transform: translateX(-100%); transition: transform .2s; box-shadow: var(--shadow-lg); }
  .sidebar.open { transform: none; }
  .scrim { display: block; position: fixed; inset: 0; background: rgb(0 0 0 / 30%); z-index: 20; }
  .mobile-menu { display: inline-flex; position: absolute; top: 20px; left: 12px; z-index: 5; }
  .main :deep(.page) { padding-top: 64px; }
}
</style>
