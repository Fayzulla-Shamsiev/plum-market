<script setup lang="ts">
import { ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { api, type CustomerDetail } from '../../api'
import Icon from '../../components/Icon.vue'
import Modal from '../../components/Modal.vue'
import { count, date, dateTime, languageLabel, money, statusText } from '../../format'

const props = defineProps<{ id: number }>()
const emit = defineEmits<{ close: [] }>()
const router = useRouter()
const c = ref<CustomerDetail | null>(null)
const error = ref('')

watch(() => props.id, async id => {
  c.value = null
  try {
    c.value = await api.customer(id)
  } catch (e) {
    error.value = (e as Error).message
  }
}, { immediate: true })
</script>

<template>
  <Modal title="Профиль клиента" side width="460px" @close="emit('close')">
    <div v-if="error" class="error-banner">{{ error }}</div>
    <div v-if="!c" class="skeleton" style="height: 400px" />
    <div v-else class="stack">
      <div class="head">
        <span class="avatar">{{ c.fullName[0] }}</span>
        <div class="grow">
          <h2>{{ c.fullName }}</h2>
          <div class="muted num">{{ c.phone }}<span v-if="c.email"> · {{ c.email }}</span></div>
        </div>
      </div>

      <div class="row">
        <button class="btn btn-primary grow" @click="router.push({ path: '/chat', query: { customer: c.id, name: c.fullName } })">
          <Icon name="chat" />Написать в чат
        </button>
        <a class="btn" :href="`tel:${c.phone.replace(/\s/g, '')}`"><Icon name="phone" />Позвонить</a>
      </div>

      <div class="stats">
        <div><span>Заказов</span><b class="num">{{ c.stats.orders }}</b></div>
        <div><span>Потрачено</span><b class="num">{{ money(c.stats.spent) }}</b></div>
        <div><span>Средний чек</span><b class="num">{{ money(c.stats.averageOrder) }}</b></div>
        <div><span>Бонусный баланс</span><b class="num">{{ count(c.bonusPoints) }}</b></div>
      </div>

      <dl class="kv">
        <dt>Язык</dt><dd>{{ languageLabel[c.language] ?? c.language }}</dd>
        <dt v-if="c.country">Страна</dt><dd v-if="c.country">{{ c.country }}</dd>
        <dt v-if="c.birthDate">Дата рождения</dt><dd v-if="c.birthDate" class="num">{{ date(c.birthDate) }}</dd>
        <dt v-if="c.gender">Пол</dt><dd v-if="c.gender">{{ c.gender === 'male' ? 'Мужской' : 'Женский' }}</dd>
        <dt>Уведомления</dt><dd>{{ c.notifyOrders ? 'статус заказа' : 'без статусов' }} · {{ c.notifyPromos ? 'акции' : 'без акций' }}</dd>
        <dt>Добавлен</dt><dd class="num">{{ date(c.createdAt) }}</dd>
        <dt>Последний визит</dt><dd class="num">{{ dateTime(c.lastVisitAt) }}</dd>
        <dt>Всего начислено баллов</dt><dd class="num">{{ count(c.stats.bonusEarned) }}</dd>
      </dl>

      <section>
        <h3>Последние заказы</h3>
        <ul class="orders">
          <li v-for="o in c.orders" :key="o.id">
            <span class="num"><b>#{{ o.id }}</b></span>
            <span class="faint num">{{ dateTime(o.createdAt) }}</span>
            <span class="badge" :class="o.status">{{ statusText(o.status, o.deliveryType) }}</span>
            <span class="num right">{{ money(o.total) }}</span>
          </li>
          <li v-if="!c.orders.length" class="faint">Заказов пока нет</li>
        </ul>
      </section>
    </div>
  </Modal>
</template>

<style scoped>
.stack { display: flex; flex-direction: column; gap: 18px; }
.head { display: flex; align-items: center; gap: 12px; }
.avatar { display: grid; place-items: center; width: 48px; height: 48px; border-radius: 50%; background: var(--plum-100); color: var(--plum-700); font-weight: 700; font-size: 20px; flex: none; }
.stats { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
.stats div { border: 1px solid var(--border); border-radius: 10px; padding: 10px 12px; display: flex; flex-direction: column; }
.stats span { font-size: 12px; color: var(--text-3); }
.stats b { font-size: 16px; }
.kv { display: grid; grid-template-columns: auto 1fr; gap: 6px 16px; margin: 0; }
.kv dt { color: var(--text-3); }
.kv dd { margin: 0; text-align: right; }
h3 { margin-bottom: 8px; }
.orders { list-style: none; padding: 0; margin: 0; }
.orders li { display: grid; grid-template-columns: 60px 1fr auto 110px; gap: 8px; align-items: center; padding: 8px 0; border-bottom: 1px dashed var(--border); }
.right { text-align: right; }
</style>
