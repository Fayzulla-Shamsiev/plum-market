<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import './shop.css'
import CategoryMenu from './components/CategoryMenu.vue'
import ChatDrawer from './components/ChatDrawer.vue'
import LoginModal from './components/LoginModal.vue'
import SearchBox from './components/SearchBox.vue'
import SIcon from './components/SIcon.vue'
import Toast from './components/Toast.vue'
import { lang, langs, setLang, t } from './i18n'
import { shopApi } from './api'
import { loginOpen, me, signedIn } from './state/auth'
import { cartCount } from './state/cart'
import { chatOpen } from './state/chat'
import { favoritesCount } from './state/favorites'
import { rootOf, useMeta } from './store'
import { inTelegram, telegramInitData } from './telegram'
import { emojiFor } from './visuals'

const route = useRoute()
const router = useRouter()
const meta = useMeta()
const menuOpen = ref(false)
const langOpen = ref(false)

// The chip bar highlights the top-level category you're currently inside.
const activeRoot = computed(() => {
  const id = Number(route.params.id)
  return route.path.startsWith('/catalog/') && meta.value ? rootOf(meta.value.categories, id)?.id : undefined
})

// Old quick-view links (?product=ID) now open the product page.
watch(() => route.query.product, id => { if (id) router.replace(`/product/${id}`) }, { immediate: true })

// Floating "back to top" once the shopper has scrolled a couple of screens.
const scrolled = ref(false)
const onScroll = () => { scrolled.value = window.scrollY > 900 }
onMounted(() => window.addEventListener('scroll', onScroll, { passive: true }))
onBeforeUnmount(() => window.removeEventListener('scroll', onScroll))
const toTop = () => window.scrollTo({ top: 0, behavior: 'smooth' })


watch(() => route.path, () => { menuOpen.value = false; chatOpen.value = false })
// Restore the signed-in customer (name in the header) after a reload.
watch(signedIn, s => { if (s && !me.value) shopApi.me().then(m => { me.value = m }).catch(() => {}) }, { immediate: true })
// Inside the bot, tie the account to this Telegram chat so order updates can arrive there too.
watch(signedIn, s => {
  if (s && telegramInitData.value) shopApi.linkTelegram(telegramInitData.value).catch(() => {})
}, { immediate: true })

const adminTitle = document.title
watch(() => meta.value?.store.name, n => { if (n) document.title = n }, { immediate: true })
onBeforeUnmount(() => { document.title = adminTitle })
</script>

<template>
  <div class="shop">
    <header class="top">
      <div class="s-wrap bar">
        <RouterLink to="/" class="logo" :aria-label="t('home')">
          <svg viewBox="0 0 40 40" width="40" height="40" aria-hidden="true">
            <rect width="40" height="40" rx="12" fill="#1f7aec" />
            <path d="M14 29V12.5h7.2a6.2 6.2 0 0 1 0 12.4H14" fill="none" stroke="#fff" stroke-width="3.4" stroke-linecap="round" stroke-linejoin="round" />
            <circle cx="28.5" cy="28.5" r="3.2" fill="#35d07f" />
          </svg>
          <span class="logo-txt">
            <b>{{ meta?.store.name ?? 'Plum Market' }}</b>
            <small>Plum Market</small>
          </span>
        </RouterLink>

        <button class="catalog-btn" :class="{ on: menuOpen }" :aria-expanded="menuOpen" @click="menuOpen = !menuOpen">
          <SIcon :name="menuOpen ? 'close' : 'grid'" :size="18" />
          <span>{{ t('catalog') }}</span>
        </button>

        <SearchBox />

        <nav class="icons">
          <RouterLink to="/favorites" class="icon-link" :aria-label="t('favorites')">
            <SIcon name="heart" :size="22" />
            <span v-if="favoritesCount" class="dot">{{ favoritesCount }}</span>
            <small>{{ t('favorites') }}</small>
          </RouterLink>
          <RouterLink to="/cart" class="icon-link" :aria-label="t('cart')">
            <SIcon name="cart" :size="22" />
            <span v-if="cartCount" class="dot blue">{{ cartCount > 99 ? '99+' : cartCount }}</span>
            <small>{{ t('cart') }}</small>
          </RouterLink>
          <RouterLink to="/profile" class="icon-link" :aria-label="t('profile')">
            <span v-if="me" class="me">{{ me.firstName[0] ?? '·' }}</span>
            <SIcon v-else name="user" :size="22" />
            <small>{{ me?.firstName || t('profile') }}</small>
          </RouterLink>
        </nav>

        <div class="lang" @focusout="e => !(e.currentTarget as HTMLElement).contains(e.relatedTarget as Node) && (langOpen = false)">
          <button class="lang-btn" :aria-expanded="langOpen" aria-haspopup="listbox" @click="langOpen = !langOpen">
            <SIcon name="globe" :size="18" />
            <span>{{ langs.find(l => l.code === lang)?.short }}</span>
          </button>
          <div v-if="langOpen" class="lang-pop" role="listbox">
            <button v-for="l in langs" :key="l.code" role="option" :aria-selected="l.code === lang" :class="{ on: l.code === lang }"
              @click="setLang(l.code); langOpen = false">{{ l.label }}</button>
          </div>
        </div>
      </div>

      <!-- Persistent category rail: always one tap away while scrolling. -->
      <nav class="rail">
        <div class="s-wrap">
          <div class="s-scroll chips">
            <RouterLink to="/" class="s-chip" :class="{ active: route.path === '/' }">
              <SIcon name="home" :size="16" /> {{ t('home') }}
            </RouterLink>
            <RouterLink v-for="c in meta?.categories ?? []" :key="c.id" :to="`/catalog/${c.id}`" class="s-chip"
              :class="{ active: activeRoot === c.id }">
              <span aria-hidden="true">{{ emojiFor(c.name) }}</span> {{ c.name }}
            </RouterLink>
          </div>
        </div>
      </nav>
    </header>

    <main class="content">
      <RouterView />
    </main>

    <footer class="foot">
      <div class="s-wrap foot-grid">
        <div>
          <b>{{ meta?.store.name }}</b>
          <p v-if="meta?.categories.length">{{ t('catalog') }}: {{ meta.categories.map(c => c.name).join(' · ') }}</p>
        </div>
        <ul>
          <li v-for="b in meta?.branches ?? []" :key="b.id">
            <SIcon name="store" :size="15" /> <span><b>{{ b.name }}</b><template v-if="b.address"> — {{ b.address }}</template></span>
          </li>
        </ul>
        <nav class="foot-links">
          <RouterLink to="/about">{{ t('about') }}</RouterLink>
          <RouterLink to="/delivery-terms">{{ t('deliveryTerms') }}</RouterLink>
          <RouterLink to="/returns">{{ t('returnTerms') }}</RouterLink>
          <RouterLink to="/contact">{{ t('contact') }}</RouterLink>
        </nav>
        <div class="powered">
          <span>Работает на Plum Market</span>
          <RouterLink v-if="!inTelegram" to="/dashboard">Кабинет продавца →</RouterLink>
        </div>
      </div>
    </footer>

    <!-- Phone: persistent bottom navigation -->
    <nav class="tabbar" aria-label="Навигация">
      <RouterLink to="/" class="tab" :class="{ on: route.path === '/' && !menuOpen }"><SIcon name="home" /><span>{{ t('home') }}</span></RouterLink>
      <button class="tab" :class="{ on: menuOpen || route.path.startsWith('/catalog') }" @click="menuOpen = true"><SIcon name="grid" /><span>{{ t('catalog') }}</span></button>
      <RouterLink to="/favorites" class="tab" :class="{ on: route.path === '/favorites' && !menuOpen }">
        <span class="tab-ico"><SIcon name="heart" /><i v-if="favoritesCount">{{ favoritesCount }}</i></span><span>{{ t('favorites') }}</span>
      </RouterLink>
      <RouterLink to="/cart" class="tab" :class="{ on: route.path === '/cart' && !menuOpen }">
        <span class="tab-ico"><SIcon name="cart" /><i v-if="cartCount" class="blue">{{ cartCount > 99 ? '99+' : cartCount }}</i></span><span>{{ t('cart') }}</span>
      </RouterLink>
      <RouterLink to="/profile" class="tab" :class="{ on: route.path.startsWith('/profile') && !menuOpen }">
        <span class="tab-ico"><span v-if="me" class="me sm">{{ me.firstName[0] ?? '·' }}</span><SIcon v-else name="user" /></span><span>{{ t('profile') }}</span>
      </RouterLink>
    </nav>

    <button v-show="scrolled" class="to-top" aria-label="Наверх" @click="toTop"><SIcon name="chevronDown" style="transform: rotate(180deg)" /></button>

    <CategoryMenu v-if="menuOpen && meta" :categories="meta.categories" @close="menuOpen = false" />
    <ChatDrawer v-if="chatOpen" />
    <LoginModal v-if="loginOpen" />
    <Toast />
  </div>
</template>

<style scoped>
.top { position: sticky; top: 0; z-index: 50; background: rgb(255 255 255 / 92%); backdrop-filter: saturate(1.4) blur(14px); box-shadow: 0 1px 0 var(--line); }
.bar { display: flex; align-items: center; gap: 16px; height: 76px; }
.logo { display: flex; align-items: center; gap: 10px; flex: none; }
.logo-txt { display: flex; flex-direction: column; line-height: 1.15; }
.logo-txt b { font-size: 17px; letter-spacing: -0.01em; }
.logo-txt small { font-size: 12px; color: var(--blue); font-weight: 600; }
.catalog-btn {
  display: inline-flex; align-items: center; gap: 8px; height: 46px; padding: 0 18px; border-radius: 14px; border: 0; flex: none;
  background: var(--blue); color: #fff; font-weight: 600; cursor: pointer; transition: background .15s;
}
.catalog-btn:hover, .catalog-btn.on { background: var(--blue-600); }
.icons { display: flex; gap: 4px; margin-left: auto; }
.icon-link { position: relative; display: flex; flex-direction: column; align-items: center; gap: 2px; padding: 4px 10px; border-radius: 12px; color: var(--ink-2); }
.icon-link small { font-size: 11.5px; font-weight: 600; }
.icon-link:hover, .icon-link.router-link-active { color: var(--blue); }
.dot {
  position: absolute; top: 0; left: calc(50% + 6px); min-width: 18px; height: 18px; padding: 0 5px; border-radius: 9px;
  background: var(--red); color: #fff; font-size: 11px; font-weight: 700; display: grid; place-items: center; border: 2px solid #fff;
}
.dot.blue { background: var(--blue); }
.me { width: 24px; height: 24px; border-radius: 50%; background: var(--blue); color: #fff; display: grid; place-items: center; font-size: 12.5px; font-weight: 700; }
.me.sm { width: 22px; height: 22px; font-size: 11.5px; }
.icon-link small { max-width: 72px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.lang { position: relative; }
.lang-btn { display: inline-flex; align-items: center; gap: 6px; height: 46px; padding: 0 12px; border-radius: 14px; border: 1px solid var(--line); background: var(--card); cursor: pointer; font-weight: 600; color: var(--ink-2); }
.lang-btn:hover { border-color: var(--blue-100); color: var(--ink); }
.lang-pop { position: absolute; right: 0; top: calc(100% + 8px); background: var(--card); border-radius: 14px; box-shadow: var(--lift-lg); border: 1px solid var(--line); padding: 6px; min-width: 160px; z-index: 70; }
.lang-pop button { display: block; width: 100%; text-align: left; border: 0; background: none; padding: 9px 12px; border-radius: 9px; cursor: pointer; }
.lang-pop button:hover { background: var(--page); }
.lang-pop button.on { color: var(--blue); font-weight: 600; }

.rail { border-top: 1px solid var(--line); }
.chips { padding: 9px 0; }
.chips .s-chip { height: 34px; background: transparent; border-color: transparent; }
.chips .s-chip:hover { background: var(--page); }
.chips .s-chip.active { background: var(--ink); color: #fff; }

.content { min-height: 60vh; padding-bottom: 40px; }

.foot { background: var(--card); border-top: 1px solid var(--line); margin-top: 48px; padding: 32px 0; color: var(--ink-2); font-size: 14px; }
.foot-grid { display: grid; grid-template-columns: 1.1fr 1.4fr 1fr 1fr; gap: 28px; }
.foot-links { display: flex; flex-direction: column; gap: 8px; }
.foot-links a:hover { color: var(--blue); }
.foot p { margin: 6px 0 0; }
.foot ul { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 8px; }
.foot li { display: flex; gap: 8px; align-items: flex-start; }
.foot li svg { margin-top: 3px; flex: none; color: var(--blue); }
.powered { display: flex; flex-direction: column; gap: 6px; align-items: flex-end; color: var(--ink-3); }
.powered a { color: var(--blue); font-weight: 600; }

.tabbar { display: none; }
.to-top {
  position: fixed; right: 20px; bottom: 24px; z-index: 40; width: 46px; height: 46px; border-radius: 50%; border: 0;
  background: var(--card); box-shadow: var(--lift-lg); cursor: pointer; display: grid; place-items: center; color: var(--ink);
}
.to-top:hover { color: var(--blue); }

@media (max-width: 900px) {
  .logo-txt { display: none; }
  .catalog-btn span { display: none; }
  .catalog-btn { padding: 0 13px; }
  .foot-grid { grid-template-columns: 1fr; }
  .powered { align-items: flex-start; }
}
@media (max-width: 640px) {
  .bar { height: 64px; gap: 10px; }
  .logo svg { width: 36px; height: 36px; }
  .catalog-btn { display: none; }
  .icons { display: none; }
  .lang-btn { height: 44px; padding: 0 10px; }
  .lang-btn svg { display: none; }
  .chips { padding: 6px 0 8px; }
  .content { padding-bottom: 84px; }
  .foot { padding-bottom: 96px; }
  .tabbar {
    display: grid; grid-template-columns: repeat(5, 1fr); position: fixed; left: 0; right: 0; bottom: 0; z-index: 85;
    background: rgb(255 255 255 / 96%); backdrop-filter: blur(12px); border-top: 1px solid var(--line);
    padding: 6px 8px calc(6px + env(safe-area-inset-bottom));
  }
  .tab { display: flex; flex-direction: column; align-items: center; gap: 3px; border: 0; background: none; padding: 6px 0; font-size: 11.5px; font-weight: 600; color: var(--ink-3); cursor: pointer; }
  .tab.on { color: var(--blue); }
  .tab-ico { position: relative; display: grid; }
  .tab-ico i {
    position: absolute; top: -5px; left: 14px; min-width: 17px; height: 17px; padding: 0 4px; border-radius: 9px; font-style: normal;
    background: var(--red); color: #fff; font-size: 10.5px; font-weight: 700; display: grid; place-items: center; border: 2px solid #fff;
  }
  .tab-ico i.blue { background: var(--blue); }
  .to-top { bottom: 86px; right: 14px; width: 42px; height: 42px; }
}
</style>
