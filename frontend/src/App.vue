<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Icon from './components/Icon.vue'
import { admin, logout } from './auth'
import { chatUnread, newOrders, useLookups, watchChatUnread, watchNewOrders } from './store'

interface Section { path: string; label: string; icon: string; ready?: boolean; children?: { path: string; label: string }[] }

// MVP admin (spec "Административная часть"): orders and their flow, catalog, customers, dashboard, plus what the
// storefront needs from the store (chat, promo codes, banners, reviews, store info/branches) and Платформы —
// where the shop is open: the website every store has, and a Telegram bot if the merchant connects one.
const sections: Section[] = [
  { path: '/dashboard', label: 'Дашборд', icon: 'dashboard', ready: true },
  { path: '/orders', label: 'Заказы', icon: 'orders', ready: true },
  { path: '/customers', label: 'Клиенты', icon: 'customers', ready: true },
  { path: '/chat', label: 'Чат', icon: 'chat', ready: true },
  {
    path: '/products', label: 'Каталог', icon: 'products', ready: true,
    children: [
      { path: '/products/categories', label: 'Категории' },
      { path: '/products/items', label: 'Товары' },
      { path: '/products/discounts', label: 'Скидки' },
      { path: '/products/stock', label: 'Склад' },
    ],
  },
  {
    path: '/marketing', label: 'Маркетинг', icon: 'marketing', ready: true,
    children: [
      { path: '/marketing/promocodes', label: 'Промокоды' },
      { path: '/marketing/banners', label: 'Баннеры' },
      { path: '/marketing/reviews', label: 'Отзывы' },
    ],
  },
  {
    path: '/platforms', label: 'Платформы', icon: 'platforms', ready: true,
    children: [
      { path: '/platforms/website', label: 'Веб-сайт' },
      { path: '/platforms/telegram', label: 'Telegram-бот' },
    ],
  },
  { path: '/store', label: 'Магазин', icon: 'branches', ready: true },
]

const { lookups } = useLookups()
const route = useRoute()
const router = useRouter()
const menuOpen = ref(false)
// Per the spec, clicking a group ("Продукты", "Маркетинг") only expands its sub-menu; the page doesn't change.
const currentGroup = computed(() => sections.find(s => s.children && route.path.startsWith(s.path))?.path ?? null)
const expanded = ref<string | null>(currentGroup.value)

watch(() => route.fullPath, () => {
  menuOpen.value = false
  if (currentGroup.value) expanded.value = currentGroup.value
})
// The storefront and the sign-in pages bring their own layout; only the panel gets the sidebar.
const isShop = computed(() => !!route.matched[0]?.meta.shop)
const bare = computed(() => isShop.value || !!route.meta.open)
// The unread-chat badge belongs to the admin sidebar, so polling starts on the first admin page only.
const stopAdminWatch = watch(() => route.matched.length && !bare.value, admin => {
  if (!admin) return
  watchChatUnread()
  watchNewOrders()
  queueMicrotask(() => stopAdminWatch())
}, { immediate: true })

const toggle = (path: string) => (expanded.value = expanded.value === path ? null : path)

// The administrator's own shop on the web (a Telegram bot, if any, is opened from Платформы).
const storefront = computed(() => (admin.value ? `/shop/${admin.value.store.slug}` : '/login'))

/** Accounts are stored as "+998901111111"; show the number the way it was typed. */
const phone = computed(() => {
  const d = (admin.value?.phone ?? '').replace(/\D/g, '').slice(-9)
  return d.length === 9 ? `+998 ${d.slice(0, 2)} ${d.slice(2, 5)} ${d.slice(5, 7)} ${d.slice(7)}` : admin.value?.phone
})

async function signOut() {
  await logout()
  router.replace('/login')
}
</script>

<template>
  <!-- The storefront (ShopLayout) and the sign-in pages stand alone; everything else is the merchant admin. -->
  <RouterView v-if="bare" />
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
          <div class="brand-store">{{ admin?.store.name ?? lookups?.store.storeName ?? '…' }}</div>
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
            <span v-if="s.path === '/orders' && newOrders" class="badge-count new" title="Новые заказы">{{ newOrders }}</span>
            <small v-if="!s.ready">скоро</small>
          </RouterLink>
        </template>
      </nav>
      <div class="sidebar-foot">
        <RouterLink :to="storefront" class="shop-link">Открыть магазин ↗</RouterLink>
        <div class="account">
          <span class="who">
            <b>{{ admin?.name }}</b>
            <small>{{ phone }}</small>
          </span>
          <button class="logout" title="Выйти" @click="signOut">
            <Icon name="logout" />
          </button>
        </div>
        <span class="faint">Прототип · MVP</span>
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
.badge-count.new { background: #1fae66; }
.badge-count { margin-left: auto; min-width: 20px; height: 20px; padding: 0 6px; border-radius: 10px; background: #e34948; color: #fff; font-size: 11px; font-weight: 700; display: grid; place-items: center; }
.shop-link { display: block; color: #dccbe8; font-weight: 600; }
.sidebar-foot { margin-top: auto; padding: 16px 10px 0; font-size: 12px; color: #85709a; display: flex; flex-direction: column; gap: 10px; }
.account { display: flex; align-items: center; gap: 8px; padding-top: 10px; border-top: 1px solid rgb(255 255 255 / 12%); }
.who { display: flex; flex-direction: column; min-width: 0; }
.who b { color: #e9ddf1; font-size: 13px; font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.who small { color: #9c86ad; font-size: 11.5px; }
.logout { margin-left: auto; width: 30px; height: 30px; flex: none; display: grid; place-items: center; border: 0; border-radius: 8px; background: rgb(255 255 255 / 8%); color: #dccbe8; cursor: pointer; }
.logout:hover { background: rgb(255 255 255 / 16%); color: #fff; }
.logout svg { width: 16px; height: 16px; }
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
