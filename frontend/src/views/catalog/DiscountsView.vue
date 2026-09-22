<script setup lang="ts">
import { ref, watch } from 'vue'
import { catalogApi, type DiscountRow } from '../../api'
import BranchSwitcher from '../../components/BranchSwitcher.vue'
import Icon from '../../components/Icon.vue'
import { dateTime, money } from '../../format'
import { catalogBranch } from '../../store'
import DiscountModal from './DiscountModal.vue'

const rows = ref<DiscountRow[] | null>(null)
const editing = ref<DiscountRow | null | 'new'>(null)

const stateLabel = { active: 'Действует', scheduled: 'Запланирована', expired: 'Завершена', disabled: 'Выключена' }
const stateClass = { active: 'Completed', scheduled: 'New', expired: 'Cancelled', disabled: 'Cancelled' }

async function load() { rows.value = await catalogApi.discounts(catalogBranch.value) }
watch(catalogBranch, load, { immediate: true })

async function remove(d: DiscountRow) {
  if (!confirm(`Удалить скидку «${d.name}»?`)) return
  await catalogApi.deleteDiscount(d.id)
  load()
}
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>Скидки</h1>
      <BranchSwitcher />
      <button class="btn btn-primary" @click="editing = 'new'"><Icon name="plus" />Добавить скидку</button>
    </div>
    <div class="card">
      <div class="table-wrap">
        <table class="data">
          <thead>
            <tr><th>Название</th><th>Товары</th><th>Тип</th><th>Период действия</th><th>Филиал</th><th>Статус</th><th class="right">Действия</th></tr>
          </thead>
          <tbody>
            <tr v-for="d in rows" :key="d.id">
              <td>
                <b>{{ d.name }}</b>
                <div v-if="d.minOrderAmount" class="faint small">от {{ money(d.minOrderAmount) }}</div>
              </td>
              <td class="products">
                <span v-for="p in d.products.slice(0, 2)" :key="p" class="pill">{{ p }}</span>
                <span v-if="d.products.length > 2" class="faint small">+{{ d.products.length - 2 }}</span>
              </td>
              <td class="nowrap"><b class="num">{{ d.type === 'Percent' ? `−${d.value}%` : `−${money(d.value)}` }}</b>
                <div class="faint small">{{ d.type === 'Percent' ? 'процент' : 'фиксированная' }}</div></td>
              <td class="num nowrap small">{{ dateTime(d.startsAt) }}<br />— {{ dateTime(d.endsAt) }}</td>
              <td class="small">{{ d.branches.join(', ') }}</td>
              <td><span class="badge" :class="stateClass[d.state]">{{ stateLabel[d.state] }}</span></td>
              <td class="right nowrap">
                <button class="btn btn-sm btn-ghost btn-icon" title="Редактировать" aria-label="Редактировать" @click="editing = d"><Icon name="edit" /></button>
                <button class="btn btn-sm btn-ghost btn-icon btn-danger" title="Удалить" aria-label="Удалить" @click="remove(d)"><Icon name="trash" /></button>
              </td>
            </tr>
            <tr v-if="rows && !rows.length"><td colspan="7" class="empty">Скидок нет</td></tr>
            <tr v-if="!rows"><td colspan="7"><div class="skeleton" style="height: 200px" /></td></tr>
          </tbody>
        </table>
      </div>
    </div>
    <DiscountModal v-if="editing" :discount="editing === 'new' ? null : editing" @close="editing = null" @saved="editing = null; load()" />
  </div>
</template>

<style scoped>
.products { max-width: 320px; }
.pill { display: inline-block; font-size: 12px; background: var(--surface-2); border: 1px solid var(--border); border-radius: 10px; padding: 1px 8px; margin: 2px 4px 2px 0; }
.small { font-size: 12px; }
</style>
