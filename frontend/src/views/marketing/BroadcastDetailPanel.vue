<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { marketingApi, type BroadcastDetail } from '../../api'
import Modal from '../../components/Modal.vue'
import PlatformIcon from '../../components/PlatformIcon.vue'
import TelegramPreview from '../../components/TelegramPreview.vue'
import { count, dateTime } from '../../format'
import { useLookups } from '../../store'

const props = defineProps<{ id: number }>()
const emit = defineEmits<{ close: [] }>()
const { lookups } = useLookups()
const b = ref<BroadcastDetail | null>(null)
const filter = ref<'all' | 'Sent' | 'NotSent' | 'Blocked' | 'clicked'>('all')

onMounted(async () => { b.value = await marketingApi.broadcast(props.id) })

const statusLabel = { Sent: 'Доставлено', NotSent: 'Не отправлено', Blocked: 'Заблокировал бота' }
const statusClass = { Sent: 'Completed', NotSent: 'Cancelled', Blocked: 'Overdue' }
const list = computed(() => (b.value?.recipients ?? []).filter(r =>
  filter.value === 'all' ? true : filter.value === 'clicked' ? !!r.clickedAt : r.status === filter.value))
const n = (s: string) => b.value?.recipients.filter(r => (s === 'clicked' ? r.clickedAt : r.status === s)).length ?? 0
</script>

<template>
  <Modal title="Рассылка" side width="620px" @close="emit('close')">
    <div v-if="!b" class="skeleton" style="height: 400px" />
    <div v-else class="stack">
      <div>
        <h2>{{ b.name }}</h2>
        <p class="faint small">{{ b.status === 'Scheduled' ? 'Будет отправлена' : 'Отправлена' }} {{ dateTime(b.sendAt) }}</p>
      </div>
      <TelegramPreview :title="lookups?.store.storeName ?? 'Бот'" :text="b.text" :image-url="b.imageUrl" :button-text="b.buttonText" />
      <p v-if="b.buttonUrl" class="small muted">Кнопка ведёт на <a :href="b.buttonUrl" target="_blank" rel="noopener">{{ b.buttonUrl }}</a> — через персональную ссылку получателя.</p>

      <div class="tabs">
        <button class="chip" :class="{ active: filter === 'all' }" @click="filter = 'all'">Все <span class="count">{{ count(b.recipients.length) }}</span></button>
        <button class="chip" :class="{ active: filter === 'Sent' }" @click="filter = 'Sent'">Доставлено <span class="count">{{ n('Sent') }}</span></button>
        <button class="chip" :class="{ active: filter === 'clicked' }" @click="filter = 'clicked'">Кликнули <span class="count">{{ n('clicked') }}</span></button>
        <button class="chip" :class="{ active: filter === 'NotSent' }" @click="filter = 'NotSent'">Не отправлено <span class="count">{{ n('NotSent') }}</span></button>
        <button class="chip" :class="{ active: filter === 'Blocked' }" @click="filter = 'Blocked'">Заблокировали <span class="count">{{ n('Blocked') }}</span></button>
      </div>
      <p v-if="filter === 'NotSent'" class="small muted">Клиенты с сайта и из Instagram не подписаны на бота — сообщение им не отправить.</p>
      <table class="data">
        <thead><tr><th>Клиент</th><th>Статус</th><th>Клик</th><th v-if="b.buttonUrl">Ссылка</th></tr></thead>
        <tbody>
          <tr v-for="r in list.slice(0, 200)" :key="r.token">
            <td><div class="row" style="gap: 6px"><PlatformIcon :platform="r.platform" />{{ r.fullName }}</div></td>
            <td><span class="badge" :class="statusClass[r.status]">{{ b.status === 'Scheduled' ? 'В очереди' : statusLabel[r.status] }}</span></td>
            <td class="small num">{{ r.clickedAt ? dateTime(r.clickedAt) : '—' }}</td>
            <td v-if="b.buttonUrl"><a v-if="r.status === 'Sent'" :href="`/r/${r.token}`" target="_blank" rel="noopener" class="small" title="Персональная ссылка из сообщения этого клиента">/r/{{ r.token }}</a></td>
          </tr>
        </tbody>
      </table>
      <p v-if="list.length > 200" class="faint small">Показаны первые 200 из {{ list.length }}</p>
    </div>
  </Modal>
</template>

<style scoped>
.stack { display: flex; flex-direction: column; gap: 14px; }
h2 { font-size: 17px; }
.tabs { display: flex; gap: 6px; flex-wrap: wrap; }
.small { font-size: 12px; margin: 0; }
</style>
