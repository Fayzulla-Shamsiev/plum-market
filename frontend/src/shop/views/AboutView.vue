<script setup lang="ts">
import BackLink from '../components/BackLink.vue'
import SIcon from '../components/SIcon.vue'
import TermsText from '../components/TermsText.vue'
import { t } from '../i18n'
import { signedIn } from '../state/auth'
import { useInfo } from '../store'

// "О нас": store name, about text, working hours, contact number and every branch with its address.
const info = useInfo()
</script>

<template>
  <div class="s-wrap narrow">
    <BackLink :to="signedIn ? '/profile' : '/'" :label="signedIn ? t('profile') : t('home')" />
    <template v-if="info">
      <section class="hero">
        <span class="logo"><SIcon name="store" :size="30" /></span>
        <div>
          <h1>{{ info.store.name }}</h1>
          <div class="facts">
            <span v-if="info.store.workingHours"><SIcon name="clock" :size="16" /> {{ info.store.workingHours }}</span>
            <a v-if="info.store.phone" :href="`tel:${info.store.phone.replace(/\s/g, '')}`"><SIcon name="phone" :size="16" /> {{ info.store.phone }}</a>
          </div>
        </div>
      </section>
      <section class="card"><TermsText :text="info.store.about" /></section>

      <h2>{{ t('ourBranches') }}</h2>
      <div class="branches">
        <article v-for="b in info.branches" :key="b.id" class="branch">
          <b>{{ b.name }}</b>
          <div class="line"><SIcon name="pin" :size="15" /> {{ b.address }}</div>
          <div v-if="b.workingHours" class="line"><SIcon name="clock" :size="15" /> {{ b.workingHours }}</div>
          <a v-if="b.phone" class="line" :href="`tel:${b.phone.replace(/\s/g, '')}`"><SIcon name="phone" :size="15" /> {{ b.phone }}</a>
          <a class="map" :href="`https://www.openstreetmap.org/?mlat=${b.lat}&mlon=${b.lng}#map=17/${b.lat}/${b.lng}`" target="_blank" rel="noopener">↗ OpenStreetMap</a>
        </article>
      </div>
    </template>
    <div v-else class="s-skel" style="height: 300px; margin-top: 16px" />
  </div>
</template>

<style scoped>
.narrow { max-width: 860px; }
.hero { display: flex; align-items: center; gap: 16px; margin: 6px 0 16px; }
.logo { width: 64px; height: 64px; border-radius: 18px; background: var(--blue); color: #fff; display: grid; place-items: center; flex: none; }
h1 { font-size: 30px; letter-spacing: -0.025em; margin: 0; }
.facts { display: flex; gap: 16px; flex-wrap: wrap; color: var(--ink-2); margin-top: 4px; }
.facts span, .facts a { display: inline-flex; align-items: center; gap: 6px; }
.facts a { color: var(--blue) !important; font-weight: 600; }
.card { background: var(--card); border-radius: 20px; padding: 22px; box-shadow: var(--lift); }
h2 { font-size: 21px; margin: 28px 0 12px; }
.branches { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 12px; }
.branch { background: var(--card); border-radius: 18px; padding: 16px; box-shadow: var(--lift); display: flex; flex-direction: column; gap: 6px; }
.branch b { font-size: 16px; }
.line { display: flex; align-items: flex-start; gap: 6px; color: var(--ink-2); font-size: 14px; }
.line svg { flex: none; margin-top: 2px; color: var(--blue); }
a.line { color: var(--blue) !important; font-weight: 600; }
.map { font-size: 13px; color: var(--ink-3) !important; margin-top: 4px; }
@media (max-width: 640px) { h1 { font-size: 24px; } .card { padding: 16px; } }
</style>
