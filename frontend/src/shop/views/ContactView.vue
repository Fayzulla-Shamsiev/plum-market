<script setup lang="ts">
import BackLink from '../components/BackLink.vue'
import SIcon from '../components/SIcon.vue'
import { t } from '../i18n'
import { openChat } from '../state/chat'
import { showToast } from '../state/toast'
import { signedIn } from '../state/auth'
import { useInfo } from '../store'

// "Связаться с нами": by phone (copy the number or call) or the built-in support chat.
const info = useInfo()
async function copy(phone: string) {
  try {
    await navigator.clipboard.writeText(phone)
    showToast(t('copied'))
  } catch {
    // Clipboard blocked (e.g. insecure context): select the number so it can be copied by hand.
    const el = document.getElementById('store-phone')
    if (el) window.getSelection()?.selectAllChildren(el)
  }
}
const tel = (p: string) => `tel:${p.replace(/\s/g, '')}`
</script>

<template>
  <div class="s-wrap narrow">
    <BackLink :to="signedIn ? '/profile' : '/'" :label="signedIn ? t('profile') : t('home')" />
    <h1>{{ t('contact') }}</h1>
    <div v-if="info" class="ways">
      <section class="card">
        <span class="ico"><SIcon name="phone" :size="24" /></span>
        <h2>{{ t('byPhone') }}</h2>
        <div class="faint">{{ t('contactPhone') }}</div>
        <div id="store-phone" class="phone">{{ info.store.phone }}</div>
        <div v-if="info.store.workingHours" class="faint small">{{ t('workingHours') }}: {{ info.store.workingHours }}</div>
        <div class="actions">
          <a class="s-btn" :href="tel(info.store.phone ?? '')"><SIcon name="phone" :size="18" /> {{ t('call') }}</a>
          <button type="button" class="s-btn ghost" @click="copy(info.store.phone ?? '')"><SIcon name="copy" :size="18" /> {{ t('copy') }}</button>
        </div>
        <ul class="branches">
          <li v-for="b in info.branches" :key="b.id"><span>{{ b.name }}</span><a :href="tel(b.phone ?? '')">{{ b.phone }}</a></li>
        </ul>
      </section>
      <section class="card">
        <span class="ico green"><SIcon name="chat" :size="24" /></span>
        <h2>{{ t('byChat') }}</h2>
        <p class="faint">{{ t('byChatHint') }}</p>
        <div class="actions">
          <button type="button" class="s-btn" @click="openChat()"><SIcon name="chat" :size="18" /> {{ t('openChat') }}</button>
        </div>
      </section>
    </div>
    <div v-else class="s-skel" style="height: 260px" />
  </div>
</template>

<style scoped>
.narrow { max-width: 860px; }
h1 { font-size: 30px; letter-spacing: -0.025em; margin: 0 0 16px; }
.ways { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; align-items: start; }
.card { background: var(--card); border-radius: 20px; padding: 22px; box-shadow: var(--lift); display: flex; flex-direction: column; gap: 6px; }
.ico { width: 48px; height: 48px; border-radius: 14px; background: var(--blue-50); color: var(--blue); display: grid; place-items: center; margin-bottom: 6px; }
.ico.green { background: var(--green-50); color: var(--green); }
h2 { font-size: 19px; margin: 0; }
.faint { color: var(--ink-3); margin: 0; }
.small { font-size: 13px; }
.phone { font-size: 26px; font-weight: 750; letter-spacing: -0.01em; user-select: all; }
.actions { display: flex; gap: 8px; flex-wrap: wrap; margin-top: 10px; }
.branches { list-style: none; padding: 12px 0 0; margin: 8px 0 0; border-top: 1px solid var(--line); display: flex; flex-direction: column; gap: 6px; font-size: 14px; }
.branches li { display: flex; justify-content: space-between; gap: 8px; }
.branches span { color: var(--ink-2); }
.branches a { color: var(--blue) !important; font-weight: 600; }
@media (max-width: 720px) { .ways { grid-template-columns: 1fr; } h1 { font-size: 24px; } }
</style>
