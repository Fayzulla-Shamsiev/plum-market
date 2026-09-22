<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { marketingApi, type BroadcastRow } from '../../api'
import Icon from '../../components/Icon.vue'
import { count, dateTime } from '../../format'
import BroadcastDetailPanel from './BroadcastDetailPanel.vue'

const rows = ref<BroadcastRow[] | null>(null)
const openId = ref<number | null>(null)

async function load() { rows.value = await marketingApi.broadcasts() }
onMounted(load)

const pct = (a: number, b: number) => (b ? `${Math.round((a / b) * 100)}%` : '—')

async function cancel(b: BroadcastRow) {
  if (!confirm(`Отменить запланированную рассылку «${b.name}»?`)) return
  await marketingApi.deleteBroadcast(b.id)
  load()
}
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>Рассылка</h1>
      <RouterLink to="/marketing/broadcasts/new" class="btn btn-primary"><Icon name="plus" />Новая рассылка</RouterLink>
    </div>
    <p class="muted intro">Сообщения клиентам через Telegram-бот магазина. Клики по кнопке считаются по персональной ссылке каждого получателя.</p>
    <div class="card">
      <div class="table-wrap">
        <table class="data">
          <thead>
            <tr>
              <th>Название</th><th class="right">Всего</th><th class="right">Отправлено</th><th class="right">Не отправлено</th>
              <th class="right">Клики</th><th class="right">Заблокировали</th><th>Дата и время</th><th></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="b in rows" :key="b.id" class="clickable" @click="openId = b.id">
              <td>
                <b>{{ b.name }}</b>
                <div v-if="b.status === 'Scheduled'" class="badge New sched">Запланирована</div>
              </td>
              <td class="right num">{{ count(b.total) }}</td>
              <td class="right num">{{ count(b.sent) }}</td>
              <td class="right num">{{ count(b.notSent) }}</td>
              <td class="right num">{{ count(b.clicks) }} <span class="faint small">{{ pct(b.clicks, b.sent) }}</span></td>
              <td class="right num">{{ count(b.blocked) }}</td>
              <td class="num nowrap">{{ dateTime(b.sendAt) }}</td>
              <td class="right" @click.stop>
                <button v-if="b.status === 'Scheduled'" class="btn btn-sm btn-ghost btn-danger" @click="cancel(b)">Отменить</button>
              </td>
            </tr>
            <tr v-if="rows && !rows.length"><td colspan="8" class="empty">Рассылок пока не было</td></tr>
            <tr v-if="!rows"><td colspan="8"><div class="skeleton" style="height: 200px" /></td></tr>
          </tbody>
        </table>
      </div>
    </div>
    <BroadcastDetailPanel v-if="openId" :id="openId" @close="openId = null; load()" />
  </div>
</template>

<style scoped>
.intro { margin: -8px 0 14px; }
.sched { margin-top: 4px; }
.small { font-size: 12px; }
</style>
