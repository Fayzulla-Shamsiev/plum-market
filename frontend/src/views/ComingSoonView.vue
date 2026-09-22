<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import Icon from '../components/Icon.vue'

const titles: Record<string, string> = {
  chat: 'Чат', products: 'Продукты', marketing: 'Маркетинг', platforms: 'Платформы', payment: 'Способ оплаты',
  delivery: 'Доставка', branches: 'Филиалы', staff: 'Сотрудники', tariff: 'Тарифный план',
  extensions: 'Расширения (Plum)', settings: 'Настройки',
}
const route = useRoute()
const key = computed(() => String(route.params.section ?? '').split('/')[0])
const title = computed(() => titles[key.value] ?? 'Раздел')
// The "Chat" button on the Clients page lands here until the chat module is built.
const chatWith = computed(() => (key.value === 'chat' ? (route.query.name as string | undefined) : undefined))
</script>

<template>
  <div class="page">
    <div class="page-head"><h1>{{ title }}</h1></div>
    <div class="card card-pad soon">
      <Icon name="zap" />
      <h2>Раздел «{{ title }}» появится в следующей итерации</h2>
      <p v-if="chatWith" class="muted">Здесь откроется диалог с клиентом <b>{{ chatWith }}</b>.</p>
      <p class="muted">В этом прототипе реализованы Дашборд, Заказы и Клиенты.</p>
      <RouterLink class="btn" to="/customers">← К клиентам</RouterLink>
    </div>
  </div>
</template>

<style scoped>
.soon { display: flex; flex-direction: column; align-items: center; text-align: center; gap: 8px; padding: 56px 24px; }
.soon > svg { width: 36px; height: 36px; color: var(--plum-500); }
.soon p { margin: 0; }
.soon .btn { margin-top: 12px; }
</style>
