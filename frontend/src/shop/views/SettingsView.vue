<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { shopApi } from '../api'
import BackLink from '../components/BackLink.vue'
import { lang, langs, setLang, t, type Lang } from '../i18n'
import { me, requireLogin, signedIn } from '../state/auth'
import { viewedIds } from '../state/viewed'
import { showToast } from '../state/toast'

// "Настройки": interface language (saved to the account too, so store messages come in that language),
// which notifications to receive, and clearing what this browser remembers.
const notifyOrders = ref(true)
const notifyPromos = ref(true)
async function load() {
  try {
    me.value = await shopApi.me()
    notifyOrders.value = me.value.notifyOrders
    notifyPromos.value = me.value.notifyPromos
  } catch { /* signed out */ }
}
onMounted(() => requireLogin(load))
watch(signedIn, s => s && load())

async function save() {
  try {
    me.value = await shopApi.updateSettings({ language: lang.value, notifyOrders: notifyOrders.value, notifyPromos: notifyPromos.value })
    showToast(t('saved'))
  } catch (e) {
    showToast((e as Error).message)
  }
}
function pickLang(l: Lang) {
  setLang(l)
  save()
}
function clearViewed() {
  viewedIds.value = []
  showToast(t('cleared'))
}
function clearSearch() {
  try { localStorage.removeItem('plum.shop.recent') } catch { /* blocked */ }
  showToast(t('cleared'))
}
</script>

<template>
  <div class="s-wrap narrow">
    <BackLink to="/profile" :label="t('profile')" />
    <h1>{{ t('settings') }}</h1>
    <template v-if="signedIn && me">
      <section class="card">
        <h2>{{ t('language') }}</h2>
        <div class="seg">
          <button v-for="l in langs" :key="l.code" type="button" :class="{ on: lang === l.code }" @click="pickLang(l.code)">{{ l.label }}</button>
        </div>
      </section>

      <section class="card">
        <h2>{{ t('notifications') }}</h2>
        <label class="row">
          <span class="grow"><b>{{ t('notifyOrders') }}</b><small>{{ t('notifyOrdersHint') }}</small></span>
          <input v-model="notifyOrders" type="checkbox" class="toggle" @change="save" />
        </label>
        <label class="row">
          <span class="grow"><b>{{ t('notifyPromos') }}</b><small>{{ t('notifyPromosHint') }}</small></span>
          <input v-model="notifyPromos" type="checkbox" class="toggle" @change="save" />
        </label>
      </section>

      <section class="card">
        <h2>{{ t('privacy') }}</h2>
        <button type="button" class="row btn" @click="clearViewed">{{ t('clearViewed') }}</button>
        <button type="button" class="row btn" @click="clearSearch">{{ t('clearSearch') }}</button>
      </section>
    </template>
  </div>
</template>

<style scoped>
.narrow { max-width: 720px; }
h1 { font-size: 30px; letter-spacing: -0.025em; margin: 0 0 18px; }
.card { background: var(--card); border-radius: 20px; padding: 18px 20px; box-shadow: var(--lift); margin-bottom: 14px; display: flex; flex-direction: column; gap: 4px; }
h2 { font-size: 14px; color: var(--ink-3); text-transform: uppercase; letter-spacing: .04em; margin: 0 0 8px; }
.seg { display: flex; gap: 8px; flex-wrap: wrap; }
.seg button { padding: 10px 16px; border-radius: 12px; border: 1.5px solid var(--line); background: var(--card); cursor: pointer; font-weight: 550; }
.seg button.on { border-color: var(--blue); background: var(--blue-50); color: var(--blue-600); }
.row { display: flex; align-items: center; gap: 14px; padding: 10px 0; cursor: pointer; }
.grow { flex: 1; display: flex; flex-direction: column; gap: 2px; }
.grow small { color: var(--ink-3); font-size: 13px; }
.btn { border: 0; background: none; font: inherit; color: var(--blue); font-weight: 600; text-align: left; }
.toggle {
  appearance: none; width: 44px; height: 26px; border-radius: 13px; background: #d5dbe5; position: relative; cursor: pointer; flex: none; transition: background .15s;
}
.toggle::after { content: ''; position: absolute; left: 3px; top: 3px; width: 20px; height: 20px; border-radius: 50%; background: #fff; box-shadow: 0 1px 2px rgb(0 0 0 / 20%); transition: transform .15s; }
.toggle:checked { background: var(--green); }
.toggle:checked::after { transform: translateX(18px); }
@media (max-width: 640px) { h1 { font-size: 24px; } }
</style>
