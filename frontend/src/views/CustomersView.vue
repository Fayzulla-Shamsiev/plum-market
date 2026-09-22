<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api, type CustomerRow, type Paged, type Platform } from '../api'
import Icon from '../components/Icon.vue'
import Pager from '../components/Pager.vue'
import PlatformIcon from '../components/PlatformIcon.vue'
import { count, date, platformLabel, relative } from '../format'
import { botLink, useLookups } from '../store'
import BonusSettingsModal from './customers/BonusSettingsModal.vue'
import CustomerDrawer from './customers/CustomerDrawer.vue'

const route = useRoute()
const router = useRouter()
const { lookups } = useLookups()

const search = ref('')
const platform = ref<Platform | ''>('')
const sort = ref('lastVisit')
const page = ref(1)
const data = ref<Paged<CustomerRow> | null>(null)
const error = ref('')
const bonusOpen = ref(false)
const openId = ref<number | null>(route.query.open ? Number(route.query.open) : null)

async function load() {
  error.value = ''
  try {
    data.value = await api.customers({ search: search.value.trim(), platform: platform.value, sort: sort.value, page: page.value })
  } catch (e) {
    error.value = (e as Error).message
  }
}

let timer = 0
watch(search, () => {
  clearTimeout(timer)
  timer = window.setTimeout(() => { page.value = 1; load() }, 300)
})
watch([platform, sort], () => { page.value = 1; load() })
watch(page, load)
onMounted(load)

// Keep ?open=<id> in the URL so a customer profile is linkable (e.g. from the dashboard's top-10).
watch(openId, id => router.replace({ query: id ? { open: id } : {} }))

const openChat = (c: CustomerRow) => router.push({ path: '/chat', query: { customer: c.id, name: c.fullName } })
</script>

<template>
  <div class="page">
    <div class="page-head">
      <h1>Клиенты</h1>
      <button class="btn" @click="bonusOpen = true"><Icon name="gift" />Настройки баллов</button>
    </div>

    <div class="card">
      <div class="toolbar">
        <label class="search">
          <Icon name="search" />
          <input v-model="search" class="input" placeholder="Имя, @username или телефон" aria-label="Поиск клиентов" />
        </label>
        <select v-model="platform" class="select" aria-label="Платформа">
          <option value="">Все платформы</option>
          <option v-for="(l, k) in platformLabel" :key="k" :value="k">{{ l }}</option>
        </select>
        <select v-model="sort" class="select" aria-label="Сортировка">
          <option value="lastVisit">Сначала недавние визиты</option>
          <option value="created">Сначала новые</option>
          <option value="orders">По количеству заказов</option>
          <option value="bonus">По бонусным баллам</option>
          <option value="name">По имени</option>
        </select>
        <span v-if="data" class="faint total num">Всего: {{ count(data.total) }}</span>
      </div>

      <div v-if="error" class="error-banner" style="margin: 12px">{{ error }}</div>

      <div class="table-wrap">
        <table class="data">
          <thead>
            <tr>
              <th>Имя клиента</th><th>Имя пользователя</th><th>Телефон</th><th class="right">Заказов</th>
              <th class="right">Бонусы</th><th>Добавлен</th><th>Последний визит</th><th>Платформа</th><th class="right">Действие</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="c in data?.items" :key="c.id" class="clickable" @click="openId = c.id">
              <td class="nowrap"><span class="avatar">{{ c.fullName[0] }}</span><b>{{ c.fullName }}</b></td>
              <td class="muted">{{ c.username ? '@' + c.username : '—' }}</td>
              <td class="num nowrap">{{ c.phone }}</td>
              <td class="right num">{{ c.orders }}</td>
              <td class="right num"><b>{{ count(c.bonusPoints) }}</b></td>
              <td class="num nowrap">{{ date(c.createdAt) }}</td>
              <td class="nowrap">{{ relative(c.lastVisitAt) }}</td>
              <td @click.stop>
                <a v-if="c.platform === 'Telegram'" :href="botLink(lookups?.store.botUsername)" target="_blank" rel="noopener"
                   class="pf-link" :title="`Открыть бота @${lookups?.store.botUsername}`">
                  <PlatformIcon :platform="c.platform" show-label />
                </a>
                <PlatformIcon v-else :platform="c.platform" show-label />
              </td>
              <td class="right" @click.stop>
                <button class="btn btn-sm" title="Перейти в чат с клиентом" @click="openChat(c)"><Icon name="chat" />Чат</button>
              </td>
            </tr>
            <tr v-if="data && !data.items.length"><td colspan="9" class="empty">Клиентов не найдено</td></tr>
            <tr v-if="!data"><td colspan="9"><div class="skeleton" style="height: 300px" /></td></tr>
          </tbody>
        </table>
      </div>
      <Pager v-if="data" v-model:page="page" :page-size="data.pageSize" :total="data.total" />
    </div>

    <BonusSettingsModal v-if="bonusOpen" @close="bonusOpen = false" />
    <CustomerDrawer v-if="openId" :id="openId" @close="openId = null" />
  </div>
</template>

<style scoped>
.toolbar { display: flex; gap: 10px; align-items: center; padding: 12px 14px; border-bottom: 1px solid var(--border); flex-wrap: wrap; }
.total { margin-left: auto; }
.avatar { display: inline-grid; place-items: center; width: 28px; height: 28px; border-radius: 50%; background: var(--plum-100); color: var(--plum-700); font-weight: 700; font-size: 12px; margin-right: 8px; }
.pf-link { text-decoration: none; color: inherit; border-radius: 6px; padding: 2px 4px; margin: -2px -4px; }
.pf-link:hover { background: #e3f2fb; text-decoration: none; }
</style>
