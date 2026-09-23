<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { marketingApi, type PromoRow } from '../../api'
import Icon from '../../components/Icon.vue'
import { count, date, money } from '../../format'
import PromoModal from './PromoModal.vue'

const rows = ref<PromoRow[] | null>(null)
const editing = ref<PromoRow | 'new' | null>(null)
const copied = ref('')

const stateLabel = { active: 'Действует', scheduled: 'Запланирован', expired: 'Истёк', exhausted: 'Лимит исчерпан', disabled: 'Выключен' }
const stateClass = { active: 'Completed', scheduled: 'New', expired: 'Cancelled', exhausted: 'OnTheWay', disabled: 'Cancelled' }

async function load() { rows.value = await marketingApi.promos() }
onMounted(load)

async function remove(p: PromoRow) {
  if (!confirm(`Удалить промокод ${p.code}?`)) return
  await marketingApi.deletePromo(p.id)
  load()
}
async function copy(code: string) {
  try { await navigator.clipboard.writeText(code); copied.value = code; setTimeout(() => (copied.value = ''), 1500) } catch { /* clipboard blocked */ }
}

// "Проверить промокод" — the same rules checkout applies.
const check = ref({ code: '', amount: 100000 })
const checkResult = ref<{ valid: boolean; error: string | null; discount: number } | null>(null)
async function runCheck() {
  checkResult.value = await marketingApi.checkPromo({ code: check.value.code, amount: check.value.amount, platform: 'Website' })
}
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>Промокоды</h1>
      <button class="btn btn-primary" @click="editing = 'new'"><Icon name="plus" />Добавить промокод</button>
    </div>

    <div class="card">
      <div class="table-wrap">
        <table class="data">
          <thead>
            <tr><th>Промокод</th><th>Скидка</th><th>Использовано</th><th>Мин. заказ</th><th>Срок действия</th><th>Ограничения</th><th>Статус</th><th class="right">Действия</th></tr>
          </thead>
          <tbody>
            <tr v-for="p in rows" :key="p.id">
              <td>
                <button class="code" :title="'Скопировать ' + p.code" @click="copy(p.code)">{{ p.code }}<Icon :name="copied === p.code ? 'check' : 'clipboard'" /></button>
              </td>
              <td class="nowrap"><b class="num">{{ p.type === 'Percent' ? `−${p.value}%` : `−${money(p.value)}` }}</b>
                <div v-if="p.maxDiscount" class="faint small">не более {{ money(p.maxDiscount) }}</div></td>
              <td class="num nowrap">
                {{ count(p.usedCount) }}<span class="faint"> / {{ p.usageLimit ? count(p.usageLimit) : '∞' }}</span>
                <div v-if="p.usageLimit" class="bar"><i :style="{ width: Math.min(100, (p.usedCount / p.usageLimit) * 100) + '%' }" /></div>
              </td>
              <td class="num nowrap">{{ p.minOrderAmount ? money(p.minOrderAmount) : '—' }}</td>
              <td class="num nowrap small">{{ date(p.startsAt) }} — {{ date(p.endsAt) }}</td>
              <td class="small">
                <span v-if="p.firstOrderOnly" class="pill">первый заказ</span>
                <span v-if="p.categoryIds.length" class="pill">категорий: {{ p.categoryIds.length }}</span>
                <span v-if="!p.firstOrderOnly && !p.categoryIds.length" class="faint">нет</span>
              </td>
              <td><span class="badge" :class="stateClass[p.state]">{{ stateLabel[p.state] }}</span></td>
              <td class="right nowrap">
                <button class="btn btn-sm btn-ghost btn-icon" title="Редактировать" aria-label="Редактировать" @click="editing = p"><Icon name="edit" /></button>
                <button class="btn btn-sm btn-ghost btn-icon btn-danger" title="Удалить" aria-label="Удалить" @click="remove(p)"><Icon name="trash" /></button>
              </td>
            </tr>
            <tr v-if="rows && !rows.length"><td colspan="8" class="empty">Промокодов нет</td></tr>
            <tr v-if="!rows"><td colspan="8"><div class="skeleton" style="height: 200px" /></td></tr>
          </tbody>
        </table>
      </div>
    </div>

    <section class="card card-pad checker">
      <h2>Проверить промокод</h2>
      <p class="muted small">Те же правила, что при оформлении заказа. Ограничение по категориям проверяется по составу корзины, поэтому здесь не учитывается.</p>
      <div class="row">
        <input v-model="check.code" class="input" placeholder="Код" aria-label="Код" @keydown.enter="runCheck" />
        <input v-model.number="check.amount" type="number" min="0" class="input w140" aria-label="Сумма заказа" />
        <span class="faint">сум</span>
        <button class="btn" :disabled="!check.code.trim()" @click="runCheck">Проверить</button>
      </div>
      <p v-if="checkResult" class="result" :class="checkResult.valid ? 'ok' : 'bad'">
        {{ checkResult.valid ? `✓ Промокод применится: скидка ${money(checkResult.discount)}, к оплате ${money(check.amount - checkResult.discount)}` : `✗ ${checkResult.error}` }}
      </p>
    </section>

    <PromoModal v-if="editing" :promo="editing === 'new' ? null : editing" @close="editing = null" @saved="editing = null; load()" />
  </div>
</template>

<style scoped>
.code { display: inline-flex; align-items: center; gap: 6px; font: 600 13px ui-monospace, SFMono-Regular, Menlo, monospace; letter-spacing: .04em; padding: 4px 8px; border-radius: 6px; border: 1px dashed var(--plum-500); background: var(--plum-50); color: var(--plum-700); cursor: pointer; }
.code svg { width: 13px; height: 13px; }
.bar { height: 4px; border-radius: 2px; background: #eeedf1; margin-top: 4px; width: 90px; overflow: hidden; }
.bar i { display: block; height: 100%; background: var(--plum-500); }
.pill { display: inline-block; font-size: 11px; background: var(--surface-2); border: 1px solid var(--border); border-radius: 10px; padding: 1px 7px; margin: 1px 4px 1px 0; }
.checker { margin-top: 16px; max-width: 900px; }
.checker h2 { margin-bottom: 2px; }
.w140 { width: 140px; }
.result { margin: 12px 0 0; padding: 8px 12px; border-radius: 8px; font-weight: 600; }
.result.ok { background: var(--good-bg); color: var(--good); }
.result.bad { background: var(--bad-bg); color: var(--bad); }
.small { font-size: 12px; }
</style>
