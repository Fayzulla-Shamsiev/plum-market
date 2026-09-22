<script setup lang="ts">
import { onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { shopApi } from '../api'
import SIcon from '../components/SIcon.vue'
import { formatPhone, t, type Key } from '../i18n'
import { me, requireLogin, signedIn, signedOut } from '../state/auth'

// Profile menu. Per the spec, opening it signed out asks for phone number and name first.
const router = useRouter()
async function load() {
  try { me.value = await shopApi.me() } catch { /* 401 handled by the API client */ }
}
onMounted(() => requireLogin(load))
watch(signedIn, s => { if (s && !me.value) load() })

type Item = { key: Key; icon: 'edit' | 'box' | 'star' | 'settings' | 'store' | 'truck' | 'refresh' | 'phone'; to: string; badge?: () => number | undefined }
const account: Item[] = [
  { key: 'editProfile', icon: 'edit', to: '/profile/edit' },
  { key: 'myOrders', icon: 'box', to: '/profile/orders', badge: () => me.value?.activeOrders || undefined },
  { key: 'myReviews', icon: 'star', to: '/profile/reviews' },
  { key: 'settings', icon: 'settings', to: '/profile/settings' },
]
const info: Item[] = [
  { key: 'about', icon: 'store', to: '/about' },
  { key: 'deliveryTerms', icon: 'truck', to: '/delivery-terms' },
  { key: 'returnTerms', icon: 'refresh', to: '/returns' },
  { key: 'contact', icon: 'phone', to: '/contact' },
]

async function logout() {
  await shopApi.logout().catch(() => {})
  signedOut()
  router.push('/')
}
</script>

<template>
  <div class="s-wrap narrow">
    <h1>{{ t('profile') }}</h1>
    <div v-if="!signedIn" class="s-empty">
      <div class="big">👤</div>
      <h3>{{ t('signInRequired') }}</h3>
      <button class="s-btn" @click="requireLogin(load)">{{ t('signIn') }}</button>
    </div>

    <template v-else-if="me">
      <RouterLink to="/profile/edit" class="who">
        <span class="ava">{{ (me.firstName[0] ?? '') + (me.lastName[0] ?? '') }}</span>
        <div class="grow">
          <b>{{ me.name }}</b>
          <div class="faint">+998 {{ formatPhone(me.phone.replace(/\D/g, '').slice(3)) }}</div>
        </div>
        <div v-if="me.bonusPoints" class="bonus"><small>{{ t('bonus') }}</small><b>{{ me.bonusPoints }}</b></div>
        <SIcon name="chevronRight" :size="18" class="chev" />
      </RouterLink>

      <nav class="menu">
        <RouterLink v-for="i in account" :key="i.key" :to="i.to" class="item">
          <SIcon :name="i.icon" /><span class="grow">{{ t(i.key) }}</span>
          <span v-if="i.badge?.()" class="pill">{{ i.badge() }}</span>
          <SIcon name="chevronRight" :size="18" class="chev" />
        </RouterLink>
      </nav>
      <nav class="menu">
        <RouterLink v-for="i in info" :key="i.key" :to="i.to" class="item">
          <SIcon :name="i.icon" /><span class="grow">{{ t(i.key) }}</span>
          <SIcon name="chevronRight" :size="18" class="chev" />
        </RouterLink>
      </nav>
      <nav class="menu">
        <button type="button" class="item danger" @click="logout">
          <SIcon name="logout" /><span class="grow">{{ t('logout') }}</span>
        </button>
      </nav>
    </template>
  </div>
</template>

<style scoped>
.narrow { max-width: 720px; }
h1 { font-size: 30px; letter-spacing: -0.025em; margin: 22px 0 18px; }
.who { display: flex; align-items: center; gap: 14px; background: var(--card); border-radius: 20px; padding: 18px; box-shadow: var(--lift); }
.ava { width: 56px; height: 56px; border-radius: 50%; background: var(--blue); color: #fff; display: grid; place-items: center; font-weight: 700; font-size: 19px; flex: none; }
.grow { flex: 1; min-width: 0; }
.who b { font-size: 18px; }
.faint { color: var(--ink-3); }
.bonus { display: flex; flex-direction: column; align-items: flex-end; background: var(--green-50); color: #0d7a45; padding: 6px 12px; border-radius: 12px; }
.bonus small { font-size: 12px; }
.menu { margin-top: 14px; background: var(--card); border-radius: 20px; box-shadow: var(--lift); overflow: hidden; }
.item { display: flex; align-items: center; gap: 14px; width: 100%; padding: 15px 18px; border: 0; background: none; cursor: pointer; font: inherit; text-align: left; border-bottom: 1px solid var(--line); }
.item:last-child { border-bottom: 0; }
.item:hover { background: var(--page); }
.item > svg:first-child { color: var(--blue); }
.chev { color: var(--ink-3); }
.pill { background: var(--blue); color: #fff; font-size: 12px; font-weight: 700; padding: 2px 8px; border-radius: 999px; }
.danger, .danger > svg:first-child { color: var(--red) !important; }
@media (max-width: 640px) { h1 { font-size: 24px; } }
</style>
