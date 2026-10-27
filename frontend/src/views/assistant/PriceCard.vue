<script setup lang="ts">
import { computed, ref } from 'vue'
import type { PriceCheck } from '../../api'
import Icon from '../../components/Icon.vue'

// Card «price»: what the product costs in Uzbek shops, found by the assistant's web search, and a suggested price.
// The administrator keeps it, types their own or taps an offer's price, then «Поставить цену» sends the choice to
// the assistant as their message — it saves it from there. Only the newest answer's card can still send.
const props = defineProps<{ title: string; price: PriceCheck; active: boolean; locked?: boolean }>()
const emit = defineEmits<{ chosen: [amount: number] }>()

/** Plain spaces (not the narrow no-break ones Intl uses), so the amount reads the same in the chat. */
const fmt = (v: number) => String(Math.round(v)).replace(/\B(?=(\d{3})+(?!\d))/g, ' ')

const raw = ref(props.price.suggested ? fmt(props.price.suggested) : '')
const sent = ref<number | null>(null)
const amount = computed(() => Number(raw.value.replace(/\D/g, '')) || 0)
const valid = computed(() => amount.value >= 1000)

function take(v: number) {
  if (sent.value === null) raw.value = fmt(v)
}
function tidy() {
  if (amount.value) raw.value = fmt(amount.value)
}
function confirm() {
  if (!valid.value || props.locked || sent.value !== null) return
  sent.value = amount.value
  emit('chosen', amount.value)
}
const condition = (c: string) => (c === 'used' ? 'б/у' : c === 'new' ? 'новый' : '')
const site = (url: string) => { try { return new URL(url).host.replace(/^www\./, '') } catch { return '' } }
</script>

<template>
  <div class="pc">
    <div class="pc-title"><Icon name="search" />{{ title }}</div>
    <p v-if="price.summary" class="summary">{{ price.summary }}</p>

    <ul class="offers">
      <li v-for="o in price.offers" :key="o.url" :class="{ used: o.condition === 'used' }">
        <div class="who">
          <b>{{ o.store }}</b>
          <em v-if="condition(o.condition)" :class="o.condition">{{ condition(o.condition) }}</em>
          <a :href="o.url" target="_blank" rel="noopener noreferrer" :title="o.title">{{ site(o.url) }} ↗</a>
        </div>
        <button type="button" class="amount" :disabled="!active || sent !== null" title="Взять эту цену" @click="take(o.price)">
          {{ fmt(o.price) }} сум
        </button>
      </li>
    </ul>

    <div v-if="sent !== null" class="done"><Icon name="check" />Цена {{ fmt(sent) }} сум отправлена помощнику</div>
    <form v-else-if="active" class="choose" @submit.prevent="confirm">
      <label>
        <span>Ваша цена{{ price.productName ? ` для «${price.productName}»` : '' }}</span>
        <span class="sum-box">
          <input v-model="raw" inputmode="numeric" autocomplete="off" @blur="tidy" />
          <small>сум</small>
        </span>
      </label>
      <button class="save" :disabled="!valid || locked">Поставить цену</button>
    </form>
    <p class="foot">
      {{ price.suggested ? `Предложено ${fmt(price.suggested)} сум — середина цен на новый товар.` : '' }}
      Цены из интернета могут быть устаревшими — проверьте по ссылкам.
    </p>
  </div>
</template>

<style scoped>
.pc { display: flex; flex-direction: column; gap: 10px; }
.pc-title { display: flex; align-items: center; gap: 8px; font-weight: 650; }
.pc-title svg { width: 17px; height: 17px; color: var(--blue); }
.summary { margin: 0; font-size: 13px; color: var(--ink-2); }
.offers { list-style: none; margin: 0; padding: 0; display: grid; gap: 2px; }
.offers li { display: flex; align-items: center; gap: 10px; padding: 7px 8px; border-radius: 10px; }
.offers li:nth-child(odd) { background: #f7f9fc; }
.offers li.used { opacity: .8; }
.who { display: flex; align-items: baseline; gap: 6px; flex-wrap: wrap; min-width: 0; }
.who b { font-weight: 600; font-size: 14px; }
.who em { font-style: normal; font-size: 11.5px; font-weight: 600; padding: 1px 7px; border-radius: 999px; background: #e3f8ec; color: #139a55; }
.who em.used { background: #fff3dc; color: #8a5a00; }
.who a { font-size: 12.5px; color: var(--ink-3); overflow-wrap: anywhere; }
.who a:hover { color: var(--blue); text-decoration: underline; }
.amount { margin-left: auto; flex: none; border: 0; background: none; padding: 4px 6px; border-radius: 8px; font: inherit; font-size: 14px; font-weight: 650; color: var(--ink); cursor: pointer; white-space: nowrap; }
.amount:hover:not(:disabled) { background: var(--blue-50); color: var(--blue); }
.amount:disabled { cursor: default; }
.choose { display: flex; align-items: flex-end; gap: 10px; flex-wrap: wrap; padding-top: 2px; }
.choose label { display: flex; flex-direction: column; gap: 4px; font-size: 12.5px; color: var(--ink-3); flex: 1; min-width: 180px; }
.sum-box { display: flex; align-items: center; height: 40px; padding: 0 12px; border: 1.5px solid var(--line); border-radius: 11px; background: #fff; transition: border-color .15s; }
.sum-box:focus-within { border-color: var(--blue); }
.sum-box input { flex: 1; min-width: 0; border: 0; outline: none; font: inherit; font-size: 15px; font-weight: 650; color: var(--ink); background: transparent; }
.sum-box small { color: var(--ink-3); font-size: 13px; }
.save { height: 40px; padding: 0 18px; border: 0; border-radius: 11px; background: var(--blue); color: #fff; font: inherit; font-size: 14px; font-weight: 650; cursor: pointer; }
.save:hover:not(:disabled) { background: var(--blue-600); }
.save:disabled { opacity: .5; cursor: default; }
.done { display: flex; align-items: center; gap: 6px; color: #139a55; font-size: 13.5px; font-weight: 600; }
.done svg { width: 15px; height: 15px; }
.foot { margin: 0; font-size: 12px; color: var(--ink-3); }
</style>
