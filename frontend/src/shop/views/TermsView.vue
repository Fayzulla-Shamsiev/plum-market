<script setup lang="ts">
import { computed } from 'vue'
import BackLink from '../components/BackLink.vue'
import SIcon from '../components/SIcon.vue'
import TermsText from '../components/TermsText.vue'
import { price, t } from '../i18n'
import { signedIn } from '../state/auth'
import { useInfo } from '../store'

// "Условия доставки" and "Условия возврата и обмена": the merchant's text, plus live numbers (fee, free threshold).
const props = defineProps<{ kind: 'delivery' | 'returns' }>()
const info = useInfo()
const text = computed(() => (props.kind === 'delivery' ? info.value?.delivery.terms : info.value?.returns.terms) ?? null)
</script>

<template>
  <div class="s-wrap narrow">
    <BackLink :to="signedIn ? '/profile' : '/'" :label="signedIn ? t('profile') : t('home')" />
    <h1>{{ kind === 'delivery' ? t('deliveryTerms') : t('returnTerms') }}</h1>
    <template v-if="info">
      <div v-if="kind === 'delivery'" class="facts">
        <div class="fact"><SIcon name="store" :size="22" /><div><b>{{ t('pickup') }}</b><span>{{ t('free') }}</span></div></div>
        <div class="fact"><SIcon name="truck" :size="22" /><div>
          <b>{{ t('delivery') }} · {{ price(info.delivery.fee) }}</b>
          <span v-if="info.delivery.freeFrom">{{ t('freeFrom') }} {{ price(info.delivery.freeFrom) }}</span>
        </div></div>
        <div class="fact"><span class="cash">💵</span><div><b>{{ t('payment') }}</b><span>{{ t('cashShort') }}</span></div></div>
      </div>
      <section class="card"><TermsText :text="text" /></section>
      <p class="more">
        <RouterLink to="/contact">{{ t('contact') }} →</RouterLink>
      </p>
    </template>
    <div v-else class="s-skel" style="height: 300px" />
  </div>
</template>

<style scoped>
.narrow { max-width: 820px; }
h1 { font-size: 30px; letter-spacing: -0.025em; margin: 0 0 16px; }
.facts { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 10px; margin-bottom: 14px; }
.fact { display: flex; gap: 12px; align-items: center; background: var(--card); border-radius: 16px; padding: 14px 16px; box-shadow: var(--lift); }
.fact svg { color: var(--blue); flex: none; }
.fact div { display: flex; flex-direction: column; }
.fact span { color: var(--ink-3); font-size: 13.5px; }
.cash { font-size: 22px; }
.card { background: var(--card); border-radius: 20px; padding: 22px; box-shadow: var(--lift); }
.more a { color: var(--blue) !important; font-weight: 600; }
@media (max-width: 640px) { h1 { font-size: 24px; } .card { padding: 16px; } }
</style>
